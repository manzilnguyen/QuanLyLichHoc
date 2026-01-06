using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using System.Text;
using System.Globalization;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- Class phụ (DTO) để hiển thị dữ liệu ra View History ---
        public class SessionViewModel
        {
            public Schedule Schedule { get; set; }
            public DateTime Date { get; set; } // Ngày cụ thể (VD: 20/10/2023)
        }

        // ============================================================
        // 1. HIỂN THỊ DANH SÁCH ĐIỂM DANH
        // ============================================================
        // Thêm tham số 'date' để biết đang điểm danh cho ngày nào
        public async Task<IActionResult> TakeAttendance(int scheduleId, DateTime? date)
        {
            var targetDate = date ?? DateTime.Today; // Nếu không truyền ngày, mặc định là hôm nay

            var schedule = await _context.Schedules
                .Include(s => s.Class)
                .Include(s => s.Subject)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null) return NotFound();

            // Truyền thông tin sang View
            ViewBag.ScheduleInfo = schedule;
            ViewBag.TargetDate = targetDate;

            // Lấy danh sách SV
            var students = await _context.Students
                .Where(s => s.ClassId == schedule.ClassId)
                .OrderBy(s => s.StudentCode)
                .ToListAsync();

            // Lấy bản ghi đã điểm danh (Đúng scheduleId VÀ đúng Ngày)
            var existingRecords = await _context.Attendances
                .Where(a => a.ScheduleId == scheduleId && a.Date.Date == targetDate.Date)
                .ToListAsync();

            ViewBag.ExistingRecords = existingRecords;

            return View(students);
        }

        // ============================================================
        // 2. LƯU KẾT QUẢ ĐIỂM DANH
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> SaveAttendance(int scheduleId, List<int> presentStudentIds, DateTime? date)
        {
            var targetDate = date ?? DateTime.Today; // Mặc định hôm nay nếu null

            var schedule = await _context.Schedules.FindAsync(scheduleId);
            if (schedule == null) return NotFound();

            var allStudents = await _context.Students
                .Where(s => s.ClassId == schedule.ClassId)
                .ToListAsync();

            // Tìm bản ghi cũ theo đúng ngày
            var existingRecords = await _context.Attendances
                .Where(a => a.ScheduleId == scheduleId && a.Date.Date == targetDate.Date)
                .ToListAsync();

            foreach (var student in allStudents)
            {
                var record = existingRecords.FirstOrDefault(r => r.StudentId == student.Id);

                // Logic: Có tick -> Approved, Không tick -> Rejected
                var newStatus = presentStudentIds.Contains(student.Id)
                                ? AttendanceStatus.Approved
                                : AttendanceStatus.Rejected;

                if (record != null)
                {
                    // Cập nhật
                    record.Status = newStatus;
                    _context.Update(record);
                }
                else
                {
                    // Tạo mới (Lưu đúng ngày targetDate)
                    var newRecord = new Attendance
                    {
                        ScheduleId = scheduleId,
                        StudentId = student.Id,
                        Date = targetDate,
                        Status = newStatus,
                        ProofImage = null
                    };
                    _context.Add(newRecord);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã lưu điểm danh ngày {targetDate:dd/MM/yyyy}!";

            return RedirectToAction("History");
        }

        // ============================================================
        // 3. DUYỆT ẢNH MINH CHỨNG
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> Approve(int attendanceId, bool isApproved)
        {
            var att = await _context.Attendances.FindAsync(attendanceId);
            if (att != null)
            {
                att.Status = isApproved ? AttendanceStatus.Approved : AttendanceStatus.Rejected;
                await _context.SaveChangesAsync();
                // Quay lại trang điểm danh đúng ngày đó
                return RedirectToAction("TakeAttendance", new { scheduleId = att.ScheduleId, date = att.Date });
            }
            return RedirectToAction("History");
        }

        // ============================================================
        // 4. LỊCH SỬ & QUẢN LÝ (LOGIC TẠO NGÀY TỪ LỊCH LẶP)
        // ============================================================
        public async Task<IActionResult> History()
        {
            var lecIdStr = User.FindFirst("LecturerId")?.Value;
            int lecId = 0;
            if (!string.IsNullOrEmpty(lecIdStr)) lecId = int.Parse(lecIdStr);

            // 1. Lấy khung lịch (Template)
            var query = _context.Schedules
                .Include(s => s.Class).Include(s => s.Subject).Include(s => s.Room)
                .AsQueryable();

            // Nếu không phải Admin thì lọc theo giảng viên
            if (!User.IsInRole("Admin"))
            {
                if (lecId == 0) return Content("Lỗi: Không tìm thấy thông tin giảng viên.");
                query = query.Where(s => s.LecturerId == lecId);
            }

            var schedules = await query.ToListAsync();

            // 2. Sinh ra các buổi học cụ thể (Session)
            // Logic: Quét từ quá khứ 30 ngày đến tương lai 7 ngày
            var sessions = new List<SessionViewModel>();
            var today = DateTime.Today;

            for (int i = -30; i <= 7; i++)
            {
                var loopDate = today.AddDays(i);

                // Tìm những lịch có Thứ (DayOfWeek) trùng với ngày đang xét
                var matchingSchedules = schedules.Where(s => s.DayOfWeek == loopDate.DayOfWeek);

                foreach (var s in matchingSchedules)
                {
                    sessions.Add(new SessionViewModel
                    {
                        Schedule = s,
                        Date = loopDate
                    });
                }
            }

            // Sắp xếp: Ngày mới nhất lên đầu, sau đó đến giờ học
            var sortedSessions = sessions
                .OrderByDescending(s => s.Date)
                .ThenBy(s => s.Schedule.StartTime)
                .ToList();

            return View(sortedSessions);
        }

        // ============================================================
        // 5. XUẤT BÁO CÁO EXCEL/CSV
        // ============================================================
        public async Task<IActionResult> Export(int scheduleId, DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;

            var schedule = await _context.Schedules
                .Include(s => s.Class).Include(s => s.Subject)
                .FirstOrDefaultAsync(s => s.Id == scheduleId);

            if (schedule == null) return NotFound();

            // Lấy dữ liệu điểm danh đúng ngày
            var attendances = await _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.ScheduleId == scheduleId && a.Date.Date == targetDate.Date)
                .ToListAsync();

            var students = await _context.Students
                .Where(s => s.ClassId == schedule.ClassId)
                .OrderBy(s => s.StudentCode)
                .ToListAsync();

            // Tạo CSV
            var sb = new StringBuilder();
            sb.AppendLine($"BAO CAO DIEM DANH");
            sb.AppendLine($"Lop: {schedule.Class.ClassName}, Mon: {schedule.Subject.SubjectName}");
            sb.AppendLine($"Ngay: {targetDate:dd/MM/yyyy}, Gio: {schedule.StartTime:hh\\:mm} - {schedule.EndTime:hh\\:mm}");
            sb.AppendLine("");

            sb.AppendLine("STT,Ma SV,Ho Ten,Trang Thai,Ghi Chu");

            int stt = 1;
            foreach (var sv in students)
            {
                var att = attendances.FirstOrDefault(a => a.StudentId == sv.Id);

                string status = "Chua diem danh";
                string note = "";

                if (att != null)
                {
                    if (att.Status == AttendanceStatus.Approved) status = "Co mat";
                    else if (att.Status == AttendanceStatus.Rejected) status = "Vang";
                    else status = "Cho duyet";

                    if (!string.IsNullOrEmpty(att.ProofImage)) note = "Co minh chung";
                }

                sb.AppendLine($"{stt++},{sv.StudentCode},{Unsign(sv.FullName)},{status},{note}");
            }

            string fileName = $"DiemDanh_{schedule.Class.ClassName}_{targetDate:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }

        // Hàm bỏ dấu tiếng Việt
        private string Unsign(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Normalize(NormalizationForm.FormD);
            var chars = s.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }
    }
}