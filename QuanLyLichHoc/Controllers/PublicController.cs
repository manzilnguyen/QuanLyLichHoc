using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using QuanLyLichHoc.Models.ViewModels;
using System.Security.Claims;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class PublicController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PublicController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ============================================================
        // 1. LỊCH CÁ NHÂN
        // ============================================================
        public async Task<IActionResult> MySchedule()
        {
            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null) return RedirectToAction("Login", "Account");

            List<Schedule> schedules = new List<Schedule>();
            string title = "Thời khóa biểu";

            if (User.IsInRole("Student"))
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == user.StudentId);
                if (student == null) student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == username);

                if (student != null)
                {
                    title = $"TKB - {student.FullName}";
                    schedules = await _context.Schedules
                        .Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer)
                        .Where(s => s.ClassId == student.ClassId)
                        .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                        .ToListAsync();
                }
            }
            else if (User.IsInRole("Lecturer"))
            {
                var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Id == user.LecturerId);
                if (lecturer == null) lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Email == username);

                if (lecturer != null)
                {
                    title = $"Lịch dạy - GV. {lecturer.FullName}";
                    schedules = await _context.Schedules
                        .Include(s => s.Subject).Include(s => s.Room).Include(s => s.Class)
                        .Where(s => s.LecturerId == lecturer.Id)
                        .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                        .ToListAsync();
                }
            }
            else if (User.IsInRole("Parent"))
            {
                string studentCode = username.Replace("PH", "");
                var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.StudentCode == studentCode);

                if (student != null)
                {
                    title = $"Lịch học của con: {student.FullName} ({student.Class?.ClassName})";
                    schedules = await _context.Schedules
                        .Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer)
                        .Where(s => s.ClassId == student.ClassId)
                        .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                        .ToListAsync();
                }
                else
                {
                    ViewBag.Message = "Không tìm thấy thông tin học sinh liên kết.";
                }
            }

            ViewBag.ScheduleTitle = title;
            return View(schedules);
        }

        // ============================================================
        // 2. API LẤY DỮ LIỆU CALENDAR
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetMyEvents()
        {
            var events = new List<object>();
            IQueryable<Schedule> query = _context.Schedules
                .Include(s => s.Class).Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer);

            if (User.IsInRole("Lecturer"))
            {
                var id = User.FindFirst("LecturerId")?.Value;
                if (id != null) query = query.Where(s => s.LecturerId == int.Parse(id));
            }
            else if (User.IsInRole("Student"))
            {
                var id = User.FindFirst("ClassId")?.Value;
                if (id != null) query = query.Where(s => s.ClassId == int.Parse(id));
            }
            else if (User.IsInRole("Parent"))
            {
                var username = User.Identity.Name;
                string studentCode = username.Replace("PH", "");
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode);

                if (student != null) query = query.Where(s => s.ClassId == student.ClassId);
                else return Json(new List<object>());
            }

            var schedules = await query.ToListAsync();

            foreach (var item in schedules)
            {
                var today = DateTime.Today;
                var currentDayOfWeek = (int)today.DayOfWeek;
                var targetDayOfWeek = (int)item.DayOfWeek;
                int cSharpTargetDay = (targetDayOfWeek == 8) ? 0 : targetDayOfWeek - 1;
                int diff = cSharpTargetDay - currentDayOfWeek;
                var date = today.AddDays(diff);

                events.Add(new
                {
                    id = item.Id,
                    title = $"{item.Subject.SubjectName}\n({item.Room.RoomName})",
                    start = date.ToString("yyyy-MM-dd") + "T" + item.StartTime.ToString(@"hh\:mm"),
                    end = date.ToString("yyyy-MM-dd") + "T" + item.EndTime.ToString(@"hh\:mm"),
                    color = (User.IsInRole("Student") || User.IsInRole("Parent")) ? "#3788d8" : "#1cc88a",
                    extendedProps = new { lecturer = item.Lecturer?.FullName, className = item.Class?.ClassName }
                });
            }
            return Json(events);
        }

        // ============================================================
        // 3. CHECK-IN
        // ============================================================
        public IActionResult CheckIn(int scheduleId)
        {
            ViewBag.ScheduleId = scheduleId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckIn(int scheduleId, IFormFile proofImage)
        {
            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            if (stuIdClaim == null) return RedirectToAction("MySchedule");
            int stuId = int.Parse(stuIdClaim);

            if (proofImage != null && proofImage.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid() + "_" + proofImage.FileName;
                using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create))
                {
                    await proofImage.CopyToAsync(stream);
                }

                var today = DateTime.Today;
                var existing = await _context.Attendances.FirstOrDefaultAsync(a => a.ScheduleId == scheduleId && a.StudentId == stuId && a.Date == today);

                if (existing != null)
                {
                    existing.ProofImage = "/uploads/" + uniqueFileName;
                    _context.Update(existing);
                }
                else
                {
                    _context.Add(new Attendance
                    {
                        ScheduleId = scheduleId,
                        StudentId = stuId,
                        Date = today,
                        ProofImage = "/uploads/" + uniqueFileName,
                        Status = AttendanceStatus.Pending
                    });
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "Check-in thành công!";
                return RedirectToAction("MySchedule");
            }
            return View();
        }

        // ============================================================
        // 4. KẾT QUẢ HỌC TẬP
        // ============================================================
        public async Task<IActionResult> MyGrades()
        {
            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            if (stuIdClaim == null) return NotFound();
            int stuId = int.Parse(stuIdClaim);

            var grades = await _context.Grades
                .Include(g => g.Subject).Include(g => g.Student)
                .Where(g => g.StudentId == stuId).ToListAsync();

            var hk1 = grades.Where(g => g.Semester == "HK1-2025").ToList();
            var hk2 = grades.Where(g => g.Semester == "HK2-2025").ToList();

            double gpa1 = hk1.Any() ? Math.Round(hk1.Average(g => g.Score), 1) : 0;
            double gpa2 = hk2.Any() ? Math.Round(hk2.Average(g => g.Score), 1) : 0;
            double gpaYear = (hk1.Any() || hk2.Any()) ? Math.Round((gpa1 + gpa2 * 2) / 3, 1) : 0;

            var vm = new StudentResultViewModel
            {
                Student = await _context.Students.FindAsync(stuId),
                Grades = grades,
                GpaHK1 = gpa1,
                GpaHK2 = gpa2,
                GpaYear = gpaYear,
                RankHK1 = Rank(gpa1),
                RankHK2 = Rank(gpa2),
                RankYear = Rank(gpaYear)
            };
            return View(vm);
        }
        private string Rank(double s) => s >= 9 ? "Xuất sắc" : (s >= 8 ? "Giỏi" : (s >= 6.5 ? "Khá" : (s >= 5 ? "TB" : "Yếu")));

        // ============================================================
        // 5. HỒ SƠ CÁ NHÂN
        // ============================================================
        public async Task<IActionResult> Profile()
        {
            if (User.IsInRole("Lecturer"))
            {
                var id = int.Parse(User.FindFirst("LecturerId").Value);
                return View("ProfileLecturer", await _context.Lecturers.FindAsync(id));
            }
            if (User.IsInRole("Student"))
            {
                var id = int.Parse(User.FindFirst("StudentId").Value);
                return View("ProfileStudent", await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.Id == id));
            }
            if (User.IsInRole("Parent"))
            {
                string studentCode = User.Identity.Name.Replace("PH", "");
                var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.StudentCode == studentCode);
                if (student == null) return NotFound();
                return View("ProfileParent", student);
            }
            return NotFound();
        }

        public async Task<IActionResult> EditProfile()
        {
            if (User.IsInRole("Lecturer"))
            {
                var id = int.Parse(User.FindFirst("LecturerId").Value);
                return View(await _context.Lecturers.FindAsync(id));
            }
            if (User.IsInRole("Student"))
            {
                var id = int.Parse(User.FindFirst("StudentId").Value);
                return View(await _context.Students.FindAsync(id));
            }
            if (User.IsInRole("Parent"))
            {
                string studentCode = User.Identity.Name.Replace("PH", "");
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode);
                return View(student);
            }
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string address, string phoneNumber, string email, string parentName, string parentPhone, IFormFile avatarFile)
        {
            string avatarPath = null;
            if (avatarFile != null)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + avatarFile.FileName;
                using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueFileName), FileMode.Create)) { await avatarFile.CopyToAsync(stream); }
                avatarPath = "/avatars/" + uniqueFileName;
            }

            if (User.IsInRole("Lecturer"))
            {
                var id = int.Parse(User.FindFirst("LecturerId").Value);
                var user = await _context.Lecturers.FindAsync(id);
                if (user != null)
                {
                    user.Address = address; user.PhoneNumber = phoneNumber; user.Email = email;
                    if (avatarPath != null) user.Avatar = avatarPath;
                    await _context.SaveChangesAsync();
                }
            }
            else if (User.IsInRole("Student"))
            {
                var id = int.Parse(User.FindFirst("StudentId").Value);
                var user = await _context.Students.FindAsync(id);
                if (user != null)
                {
                    user.Address = address; user.PhoneNumber = phoneNumber;
                    user.ParentName = parentName; user.ParentPhone = parentPhone;
                    if (avatarPath != null) user.Avatar = avatarPath;
                    await _context.SaveChangesAsync();
                }
            }
            else if (User.IsInRole("Parent"))
            {
                string studentCode = User.Identity.Name.Replace("PH", "");
                var user = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode);
                if (user != null)
                {
                    user.ParentName = parentName; user.ParentPhone = parentPhone; user.Address = address;
                    if (avatarPath != null) user.Avatar = avatarPath;
                    await _context.SaveChangesAsync();
                }
            }
            TempData["Success"] = "Cập nhật hồ sơ thành công!";
            return RedirectToAction("Profile");
        }

        // ============================================================
        // 6. API LẤY DANH SÁCH THÔNG BÁO (ĐÃ SỬA LỖI GỘP DỮ LIỆU)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null) return Json(new List<object>());

            // 1. Lấy thông báo cá nhân (Notification)
            // Chọn về dạng anonymous type với các trường tường minh
            var personalList = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .Select(n => new
                {
                    Title = n.Title,
                    Message = n.Message,
                    Url = n.Url ?? "#",
                    Type = n.Type,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead
                })
                .ToListAsync();

            // 2. Lấy thông báo hệ thống (SystemNotification)
            var systemList = await _context.SystemNotifications
                .Select(n => new
                {
                    Title = n.Title,
                    Message = n.Content, // Map Content -> Message
                    Url = "#",           // SystemNoti thường không có Url
                    Type = n.Type,
                    CreatedAt = n.CreatedAt,
                    IsRead = false       // Mặc định là chưa đọc
                })
                .ToListAsync();

            // 3. Gộp 2 danh sách và sắp xếp giảm dần theo ngày
            var mergedList = personalList.Concat(systemList)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20) // Lấy 20 tin mới nhất
                .Select(x => new
                {
                    title = x.Title,
                    message = x.Message,
                    url = x.Url,
                    type = x.Type,
                    time = x.CreatedAt.ToString("HH:mm dd/MM"),
                    isRead = x.IsRead
                });

            return Json(mergedList);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead()
        {
            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
            if (user != null)
            {
                var unread = await _context.Notifications.Where(n => n.UserId == user.Id && !n.IsRead).ToListAsync();
                foreach (var item in unread) item.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }
    }
}