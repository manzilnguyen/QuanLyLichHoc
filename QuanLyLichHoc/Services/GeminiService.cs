using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuanLyLichHoc.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent";
        private readonly ApplicationDbContext _context;

        public GeminiService(IConfiguration config, ApplicationDbContext context)
        {
            _apiKey = config["Gemini:ApiKey"] ?? "";
            _context = context;
        }

        public async Task<string> GetAnswer(string userQuestion, string username)
        {
            if (string.IsNullOrEmpty(_apiKey)) return "⚠️ Lỗi: Chưa cấu hình API Key.";

            var user = await _context.AppUsers
                .Include(u => u.Student).ThenInclude(s => s.Class)
                .Include(u => u.Lecturer)
                .FirstOrDefaultAsync(u => u.Username == username);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Vai trò: Bạn là EduBot. Trả lời ngắn gọn, chính xác.");
            sb.AppendLine($"Thời gian hiện tại hệ thống: {DateTime.Now:dd/MM/yyyy HH:mm} (Thứ {(int)DateTime.Now.DayOfWeek + 1})");

            if (user != null)
            {
                string role = user.Role ?? "Guest";
                string displayName = user.Username;

                if (role == "Student" && user.Student != null)
                {
                    displayName = user.Student.FullName ?? displayName;
                    await BuildStudentContext(sb, user.Student);
                }
                else if (role == "Lecturer" && user.Lecturer != null)
                {
                    displayName = user.Lecturer.FullName ?? displayName;
                    await BuildLecturerContext(sb, user.Lecturer.Id, user.Lecturer);
                }
                else if (role == "Admin" || role == "Manager")
                {
                    await BuildAdminContext(sb);
                }
                sb.Insert(0, $"Người hỏi: {displayName} ({role}).\n");
            }

            sb.AppendLine("\nCÂU HỎI: " + userQuestion);
            return await CallGeminiApi(sb.ToString());
        }

        // ============================================================
        // 1. NGỮ CẢNH HỌC SINH (LOGIC TÌM LỚP TIẾP THEO)
        // ============================================================
        private async Task BuildStudentContext(StringBuilder sb, Student student)
        {
            sb.AppendLine($"--- THÔNG TIN HỌC SINH ---");
            sb.AppendLine($"Tên: {student.FullName}, Lớp: {student.Class?.ClassName}");

            // 1. Điểm số (Giữ nguyên)
            var grades = await _context.Grades.Include(g => g.Subject)
                .Where(g => g.StudentId == student.Id).ToListAsync();
            if (grades.Any())
            {
                sb.AppendLine("[Bảng điểm]: " + string.Join(", ", grades.Select(g => $"{g.Subject.SubjectName}: {g.Score}")));
            }

            // 2. TÌM LỚP HỌC SẮP TỚI (THUẬT TOÁN CHÍNH XÁC)
            // Lấy toàn bộ lịch của lớp (vì lịch lặp lại hàng tuần)
            var allSchedules = await _context.Schedules
                .Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer)
                .Where(s => s.ClassId == student.ClassId)
                .ToListAsync();

            var upcoming = GetUpcomingSchedules(allSchedules, 5); // Lấy 5 lớp gần nhất

            if (upcoming.Any())
            {
                sb.AppendLine("\n[Lịch học sắp tới - Đã sắp xếp chính xác]:");
                foreach (var item in upcoming)
                {
                    // item.RealDate là ngày thực tế đã tính toán
                    string timeStr = $"{item.RealDate:dd/MM} ({item.Schedule.DayOfWeek}) lúc {item.Schedule.StartTime:hh\\:mm}";
                    sb.AppendLine($"- {item.Schedule.Subject.SubjectName}: {timeStr} tại {item.Schedule.Room.RoomName} (GV: {item.Schedule.Lecturer.FullName})");
                }
                // Gợi ý cho AI biết lớp nào là gần nhất
                sb.AppendLine($"=> Lớp học gần nhất là: {upcoming[0].Schedule.Subject.SubjectName} vào lúc {upcoming[0].RealDate:HH:mm dd/MM}.");
            }
            else
            {
                sb.AppendLine("\n[Lịch học]: Hiện chưa có lịch học nào được xếp.");
            }
        }

        // ============================================================
        // 2. NGỮ CẢNH GIẢNG VIÊN (LOGIC TƯƠNG TỰ)
        // ============================================================
        private async Task BuildLecturerContext(StringBuilder sb, int lecturerId, Lecturer lecturer)
        {
            sb.AppendLine($"--- THÔNG TIN GIẢNG VIÊN ---");
            sb.AppendLine($"Tên: {lecturer.FullName}, Khoa: {lecturer.Department}");

            var allSchedules = await _context.Schedules
                .Include(s => s.Class).Include(s => s.Subject).Include(s => s.Room)
                .Where(s => s.LecturerId == lecturerId)
                .ToListAsync();

            var upcoming = GetUpcomingSchedules(allSchedules, 5);

            if (upcoming.Any())
            {
                sb.AppendLine("\n[Lịch dạy sắp tới]:");
                foreach (var item in upcoming)
                {
                    string timeStr = $"{item.RealDate:dd/MM} ({item.Schedule.DayOfWeek}) lúc {item.Schedule.StartTime:hh\\:mm}";
                    sb.AppendLine($"- Lớp {item.Schedule.Class.ClassName}, Môn {item.Schedule.Subject.SubjectName}: {timeStr} tại {item.Schedule.Room.RoomName}");
                }
            }
        }

        private async Task BuildAdminContext(StringBuilder sb)
        {
            var count = await _context.Students.CountAsync();
            sb.AppendLine($"[Thống kê]: Tổng {count} học sinh toàn trường.");
        }

        // ============================================================
        // 3. THUẬT TOÁN TÍNH TOÁN THỜI GIAN THỰC (CORE LOGIC)
        // ============================================================

        private class RealSchedule
        {
            public Schedule Schedule { get; set; }
            public DateTime RealDate { get; set; }
        }

        private List<RealSchedule> GetUpcomingSchedules(List<Schedule> schedules, int take)
        {
            var now = DateTime.Now;
            var result = new List<RealSchedule>();

            foreach (var s in schedules)
            {
                // Tính khoảng cách ngày: (Ngày học - Hôm nay)
                // DayOfWeek: 0 (Sunday) -> 6 (Saturday)
                int daysUntil = ((int)s.DayOfWeek - (int)now.DayOfWeek);

                // Nếu daysUntil < 0: Ngày này trong tuần đã qua (Ví dụ nay Thứ 6, lịch Thứ 2) -> Phải là tuần sau (+7)
                // Nếu daysUntil == 0: Là hôm nay, cần check giờ

                if (daysUntil < 0)
                {
                    daysUntil += 7;
                }
                else if (daysUntil == 0)
                {
                    // Nếu là hôm nay nhưng giờ bắt đầu nhỏ hơn giờ hiện tại -> Đã qua -> Dời sang tuần sau
                    if (s.StartTime < now.TimeOfDay)
                    {
                        daysUntil += 7;
                    }
                }

                // Tính ra ngày thực tế (Date + Time)
                var realDateTime = now.Date.AddDays(daysUntil) + s.StartTime;

                result.Add(new RealSchedule { Schedule = s, RealDate = realDateTime });
            }

            // Sắp xếp theo thời gian thực gần nhất
            return result.OrderBy(x => x.RealDate).Take(take).ToList();
        }

        // ============================================================
        // 4. CALL API (GIỮ NGUYÊN)
        // ============================================================
        private async Task<string> CallGeminiApi(string prompt)
        {
            using (var client = new HttpClient())
            {
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                try
                {
                    var response = await client.PostAsync($"{_apiUrl}?key={_apiKey}", jsonContent);
                    var responseString = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonNode = JsonNode.Parse(responseString);
                        return jsonNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "Bot đang suy nghĩ...";
                    }
                    return "⚠️ Hệ thống AI đang bận.";
                }
                catch { return "⚠️ Lỗi kết nối AI."; }
            }
        }
    }
}