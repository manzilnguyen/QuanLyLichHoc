using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Hubs;
using QuanLyLichHoc.Models;
using ClosedXML.Excel;
using System.Data;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class SchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notiHub; // Inject NotificationHub để gửi thông báo

        public SchedulesController(ApplicationDbContext context, IHubContext<NotificationHub> notiHub)
        {
            _context = context;
            _notiHub = notiHub;
        }

        // ============================================================
        // 1. DANH SÁCH LỊCH HỌC (INDEX)
        // ============================================================
        public async Task<IActionResult> Index(string semester, int? classId, int? subjectId, int? lecturerId)
        {
            var query = _context.Schedules
                .Include(s => s.Class)
                .Include(s => s.Subject)
                .Include(s => s.Room)
                .Include(s => s.Lecturer)
                .AsQueryable();

            // Phân quyền dữ liệu
            if (User.IsInRole("Lecturer"))
            {
                var lid = User.FindFirst("LecturerId")?.Value;
                if (lid != null) query = query.Where(s => s.LecturerId == int.Parse(lid));
            }
            else if (User.IsInRole("Student"))
            {
                var cid = User.FindFirst("ClassId")?.Value;
                if (cid != null) query = query.Where(s => s.ClassId == int.Parse(cid));
            }

            // Bộ lọc tìm kiếm
            if (!string.IsNullOrEmpty(semester)) query = query.Where(s => s.Semester == semester);
            if (classId.HasValue) query = query.Where(s => s.ClassId == classId);
            if (subjectId.HasValue) query = query.Where(s => s.SubjectId == subjectId);
            if (lecturerId.HasValue) query = query.Where(s => s.LecturerId == lecturerId);

            // Dữ liệu cho Dropdown
            ViewData["Semester"] = new SelectList(await _context.Schedules.Select(s => s.Semester).Distinct().ToListAsync(), semester);
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", classId);
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "Id", "SubjectName", subjectId);
            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName", lecturerId);

            return View(await query.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync());
        }

        // ============================================================
        // 2. LỊCH BIỂU DẠNG CALENDAR & API
        // ============================================================
        public IActionResult Calendar() => View();

        public async Task<IActionResult> GetCalendarEvents()
        {
            var query = _context.Schedules.Include(s => s.Class).Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer).AsQueryable();

            if (User.IsInRole("Lecturer"))
            {
                var lid = User.FindFirst("LecturerId")?.Value;
                if (lid != null) query = query.Where(s => s.LecturerId == int.Parse(lid));
            }
            else if (User.IsInRole("Student"))
            {
                var cid = User.FindFirst("ClassId")?.Value;
                if (cid != null) query = query.Where(s => s.ClassId == int.Parse(cid));
            }

            var events = await query.Select(s => new {
                id = s.Id,
                title = $"{s.Subject.SubjectName}\n({s.Room.RoomName})",
                // Tính ngày hiển thị trong tuần hiện tại
                start = GetNextWeekday(s.DayOfWeek).ToString("yyyy-MM-dd") + "T" + s.StartTime.ToString(@"hh\:mm"),
                end = GetNextWeekday(s.DayOfWeek).ToString("yyyy-MM-dd") + "T" + s.EndTime.ToString(@"hh\:mm"),
                color = User.IsInRole("Admin") ? "#4f46e5" : "#10b981",
                description = $"Lớp: {s.Class.ClassName}<br>GV: {s.Lecturer.FullName}"
            }).ToListAsync();

            return Json(events);
        }

        private static DateTime GetNextWeekday(DayOfWeek day)
        {
            DateTime today = DateTime.Today;
            int daysToAdd = ((int)day - (int)today.DayOfWeek);
            return today.AddDays(daysToAdd);
        }

        // ============================================================
        // 3. TẠO LỊCH MỚI + GỬI THÔNG BÁO
        // ============================================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Schedule schedule)
        {
            if (schedule.EndTime <= schedule.StartTime)
                ModelState.AddModelError("EndTime", "Giờ kết thúc phải sau giờ bắt đầu.");

            if (ModelState.IsValid)
            {
                string conflictMsg = await CheckConflict(schedule);
                if (!string.IsNullOrEmpty(conflictMsg))
                {
                    ModelState.AddModelError("", conflictMsg);
                }
                else
                {
                    _context.Add(schedule);
                    await _context.SaveChangesAsync();

                    // --- [MỚI] GỬI THÔNG BÁO REAL-TIME ---
                    await NotifyClassScheduleChange(schedule, "📅 Lịch học mới được xếp");

                    TempData["Success"] = "Đã xếp lịch thành công!";
                    return RedirectToAction(nameof(Index));
                }
            }
            PopulateDropdowns(schedule);
            return View(schedule);
        }

        // ============================================================
        // 4. SỬA LỊCH + GỬI THÔNG BÁO
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null) return NotFound();
            PopulateDropdowns(schedule);
            return View(schedule);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Schedule schedule)
        {
            if (id != schedule.Id) return NotFound();

            if (ModelState.IsValid)
            {
                string conflict = await CheckConflict(schedule, id);
                if (!string.IsNullOrEmpty(conflict))
                {
                    ModelState.AddModelError("", conflict);
                    PopulateDropdowns(schedule);
                    return View(schedule);
                }

                _context.Update(schedule);
                await _context.SaveChangesAsync();

                // --- [MỚI] GỬI THÔNG BÁO THAY ĐỔI ---
                await NotifyClassScheduleChange(schedule, "⚠️ Lịch học thay đổi");

                return RedirectToAction(nameof(Index));
            }
            PopulateDropdowns(schedule);
            return View(schedule);
        }

        // ============================================================
        // 5. XÓA LỊCH + GỬI THÔNG BÁO
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _context.Schedules.Include(s => s.Class).Include(s => s.Subject).FirstOrDefaultAsync(s => s.Id == id);
            if (schedule != null)
            {
                // --- [MỚI] Báo trước khi xóa ---
                await NotifyClassScheduleChange(schedule, "❌ Hủy lịch học");

                _context.Schedules.Remove(schedule);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 6. CÁC TÍNH NĂNG NÂNG CAO (Import, Export, AutoArrange)
        // ============================================================

        [Authorize(Roles = "Admin")]
        public IActionResult Import() => View();

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file, string semester)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel.";
                return View();
            }

            int successCount = 0;
            List<string> errors = new List<string>();
            var classes = await _context.Classes.ToListAsync();
            var subjects = await _context.Subjects.ToListAsync();
            var lecturers = await _context.Lecturers.ToListAsync();
            var rooms = await _context.Rooms.ToListAsync();

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheets.First();
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                    foreach (var row in rows)
                    {
                        try
                        {
                            string className = row.Cell(1).GetValue<string>().Trim();
                            string subjectName = row.Cell(2).GetValue<string>().Trim();
                            string lecturerName = row.Cell(3).GetValue<string>().Trim();
                            string type = row.Cell(4).GetValue<string>().Trim();
                            int periods = row.Cell(5).GetValue<int>();

                            var cls = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
                            var sub = subjects.FirstOrDefault(s => s.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
                            var lec = lecturers.FirstOrDefault(l => l.FullName.Equals(lecturerName, StringComparison.OrdinalIgnoreCase));

                            if (cls == null || sub == null || lec == null)
                            {
                                errors.Add($"Dòng {row.RowNumber()}: Dữ liệu không tồn tại trong hệ thống."); continue;
                            }

                            // Thuật toán tìm slot trống đơn giản
                            bool scheduled = false;
                            var startTimes = new List<TimeSpan> { new TimeSpan(7, 0, 0), new TimeSpan(9, 0, 0), new TimeSpan(13, 0, 0), new TimeSpan(15, 0, 0) };

                            for (int d = 1; d <= 6; d++)
                            {
                                if (scheduled) break;
                                DayOfWeek day = (DayOfWeek)d;

                                foreach (var start in startTimes)
                                {
                                    TimeSpan end = start.Add(TimeSpan.FromMinutes(periods * 50));

                                    bool isBusy = await _context.Schedules.AnyAsync(s =>
                                        s.Semester == semester && s.DayOfWeek == day &&
                                        (s.ClassId == cls.Id || s.LecturerId == lec.Id) &&
                                        ((start >= s.StartTime && start < s.EndTime) || (end > s.StartTime && end <= s.EndTime)));

                                    if (isBusy) continue;

                                    RoomType rType = type.ToLower().Contains("thực hành") ? RoomType.Practice : RoomType.Theory;
                                    var busyRoomIds = await _context.Schedules.Where(s => s.Semester == semester && s.DayOfWeek == day &&
                                        ((start >= s.StartTime && start < s.EndTime) || (end > s.StartTime && end <= s.EndTime))).Select(s => s.RoomId).ToListAsync();

                                    var freeRoom = rooms.FirstOrDefault(r => !busyRoomIds.Contains(r.Id) && r.Capacity >= cls.MaxQuantity && r.Type == rType);

                                    if (freeRoom != null)
                                    {
                                        var newSchedule = new Schedule { ClassId = cls.Id, SubjectId = sub.Id, LecturerId = lec.Id, RoomId = freeRoom.Id, DayOfWeek = day, StartTime = start, EndTime = end, Semester = semester };
                                        _context.Add(newSchedule);
                                        await _context.SaveChangesAsync();
                                        successCount++;
                                        scheduled = true;
                                        break;
                                    }
                                }
                            }
                            if (!scheduled) errors.Add($"Dòng {row.RowNumber()}: Không xếp được lịch cho môn {subjectName}.");
                        }
                        catch (Exception ex) { errors.Add($"Dòng {row.RowNumber()}: {ex.Message}"); }
                    }
                }
            }
            TempData["Success"] = $"Xếp tự động thành công {successCount} lớp!";
            if (errors.Any()) TempData["ErrorDetails"] = errors;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Export(string semester, int? classId, int? subjectId, int? lecturerId)
        {
            var query = _context.Schedules.Include(s => s.Class).Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer).AsQueryable();

            if (!string.IsNullOrEmpty(semester)) query = query.Where(s => s.Semester == semester);
            if (classId.HasValue) query = query.Where(s => s.ClassId == classId);
            if (subjectId.HasValue) query = query.Where(s => s.SubjectId == subjectId);
            if (lecturerId.HasValue) query = query.Where(s => s.LecturerId == lecturerId);

            var data = await query.OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("TKB");
                worksheet.Cell(1, 1).Value = "THỜI KHÓA BIỂU";
                worksheet.Range("A1:G1").Merge().Style.Font.Bold = true;

                string[] headers = { "Thứ", "Giờ", "Lớp", "Môn", "Phòng", "GV", "Học Kỳ" };
                for (int i = 0; i < headers.Length; i++) { worksheet.Cell(3, i + 1).Value = headers[i]; worksheet.Cell(3, i + 1).Style.Font.Bold = true; }

                int r = 4;
                foreach (var item in data)
                {
                    worksheet.Cell(r, 1).Value = item.DayOfWeek.ToString();
                    worksheet.Cell(r, 2).Value = $"{item.StartTime:hh\\:mm} - {item.EndTime:hh\\:mm}";
                    worksheet.Cell(r, 3).Value = item.Class.ClassName;
                    worksheet.Cell(r, 4).Value = item.Subject.SubjectName;
                    worksheet.Cell(r, 5).Value = item.Room.RoomName;
                    worksheet.Cell(r, 6).Value = item.Lecturer.FullName;
                    worksheet.Cell(r, 7).Value = item.Semester;
                    r++;
                }
                worksheet.Columns().AdjustToContents();
                using (var stream = new MemoryStream()) { workbook.SaveAs(stream); return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TKB.xlsx"); }
            }
        }

        [Authorize(Roles = "Admin")]
        public IActionResult AutoArrange()
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AutoArrange(int classId, List<int> subjectIds)
        {
            TempData["Info"] = "Vui lòng sử dụng chức năng Import Excel để xếp lịch tự động tối ưu hơn.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetSuggestions(DayOfWeek day, TimeSpan start, TimeSpan end)
        {
            var busyRoomIds = await _context.Schedules
                .Where(s => s.DayOfWeek == day && ((start >= s.StartTime && start < s.EndTime) || (end > s.StartTime && end <= s.EndTime) || (start <= s.StartTime && end >= s.EndTime)))
                .Select(s => s.RoomId).ToListAsync();

            var busyLecturerIds = await _context.Schedules
                .Where(s => s.DayOfWeek == day && ((start >= s.StartTime && start < s.EndTime) || (end > s.StartTime && end <= s.EndTime) || (start <= s.StartTime && end >= s.EndTime)))
                .Select(s => s.LecturerId).ToListAsync();

            var freeRooms = await _context.Rooms.Where(r => !busyRoomIds.Contains(r.Id)).Select(r => new { id = r.Id, name = $"{r.RoomName} ({r.Capacity})" }).ToListAsync();
            var freeLecturers = await _context.Lecturers.Where(l => !busyLecturerIds.Contains(l.Id)).Select(l => new { id = l.Id, name = l.FullName }).ToListAsync();

            return Json(new { rooms = freeRooms, lecturers = freeLecturers });
        }

        // ============================================================
        // 7. HELPER METHODS (QUAN TRỌNG)
        // ============================================================

        // --- Gửi thông báo cho toàn bộ lớp ---
        private async Task NotifyClassScheduleChange(Schedule s, string title)
        {
            // Lấy thông tin chi tiết
            var subject = await _context.Subjects.FindAsync(s.SubjectId);
            var room = await _context.Rooms.FindAsync(s.RoomId);

            string msg = $"{subject?.SubjectName ?? "Môn học"} - Thứ {s.DayOfWeek} ({s.StartTime:hh\\:mm}) tại {room?.RoomName ?? "Phòng..."}";

            // 1. Lấy Username Sinh viên trong lớp (Có AppUser)
            var studentUsers = await _context.Students
                .Where(st => st.ClassId == s.ClassId && st.AppUser != null)
                .Select(st => st.AppUser.Username)
                .ToListAsync();

            // 2. Lấy Username Phụ huynh (Quy tắc: MãSV + PH)
            var studentCodes = await _context.Students
                .Where(st => st.ClassId == s.ClassId)
                .Select(st => st.StudentCode)
                .ToListAsync();
            var parentUsers = studentCodes.Select(code => code + "PH").ToList();

            // 3. Gộp danh sách nhận tin
            var allUsers = studentUsers.Concat(parentUsers).Distinct().ToList();

            // 4. Gửi lần lượt
            foreach (var username in allUsers)
            {
                // Loại thông báo Warning (Màu vàng) để gây chú ý
                await _notiHub.Clients.User(username).SendAsync("ReceiveNotification", title, msg, "/Public/MySchedule", "Warning");
            }
        }

        private void PopulateDropdowns(Schedule s = null)
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", s?.ClassId);
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "Id", "SubjectName", s?.SubjectId);
            ViewData["RoomId"] = new SelectList(_context.Rooms, "Id", "RoomName", s?.RoomId);
            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName", s?.LecturerId);
        }

        private async Task<string> CheckConflict(Schedule s, int? ignoreId = null)
        {
            bool roomBusy = await _context.Schedules.AnyAsync(x => x.Id != ignoreId && x.DayOfWeek == s.DayOfWeek && x.RoomId == s.RoomId &&
                ((s.StartTime >= x.StartTime && s.StartTime < x.EndTime) || (s.EndTime > x.StartTime && s.EndTime <= x.EndTime)));
            if (roomBusy) return "Phòng học bận.";

            bool lecBusy = await _context.Schedules.AnyAsync(x => x.Id != ignoreId && x.DayOfWeek == s.DayOfWeek && x.LecturerId == s.LecturerId &&
                ((s.StartTime >= x.StartTime && s.StartTime < x.EndTime) || (s.EndTime > x.StartTime && s.EndTime <= x.EndTime)));
            if (lecBusy) return "Giảng viên bận.";

            return null;
        }
    }
}