using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class LeavesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public LeavesController(ApplicationDbContext context) { _context = context; }

        // ============================================================
        // 1. DANH SÁCH (INDEX)
        // ============================================================
        public async Task<IActionResult> Index(string searchString)
        {
            var query = _context.LeaveRequests
                .Include(l => l.Student)
                .Include(l => l.Class)
                .AsQueryable();

            if (User.IsInRole("Student"))
            {
                var stuIdClaim = User.FindFirst("StudentId")?.Value;
                if (stuIdClaim != null) query = query.Where(l => l.StudentId == int.Parse(stuIdClaim));
            }
            else if (User.IsInRole("Lecturer"))
            {
                var lecIdClaim = User.FindFirst("LecturerId")?.Value;
                if (lecIdClaim != null) query = query.Where(l => l.Class.LecturerId == int.Parse(lecIdClaim));
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(l => l.Student.FullName.Contains(searchString) || l.Class.ClassName.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await query.OrderByDescending(l => l.CreatedAt).ToListAsync());
        }

        // ============================================================
        // 2. TẠO ĐƠN (CREATE - GET)
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Create()
        {
            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            if (stuIdClaim == null) return RedirectToAction("Login", "Account");
            int stuId = int.Parse(stuIdClaim);

            // Logic gộp lớp (Sinh hoạt + Tín chỉ)
            var student = await _context.Students.Include(s => s.Class).ThenInclude(c => c.Lecturer).FirstOrDefaultAsync(s => s.Id == stuId);

            var enrolledClasses = await _context.Enrollments
                .Include(e => e.Class).ThenInclude(c => c.Lecturer)
                .Where(e => e.StudentId == stuId && e.Status == EnrollmentStatus.Approved)
                .Select(e => e.Class).ToListAsync();

            var combinedClasses = new List<Class>();
            if (student.Class != null) combinedClasses.Add(student.Class);
            combinedClasses.AddRange(enrolledClasses);
            combinedClasses = combinedClasses.DistinctBy(c => c.Id).ToList();

            if (!combinedClasses.Any())
            {
                TempData["Error"] = "Bạn chưa được phân lớp hoặc chưa đăng ký lớp học nào.";
                return RedirectToAction(nameof(Index));
            }

            var selectList = combinedClasses.Select(c => new
            {
                Id = c.Id,
                DisplayName = $"{c.ClassName} (GV: {c.Lecturer?.FullName ?? "Chưa phân công"})"
            });

            ViewBag.ClassList = new SelectList(selectList, "Id", "DisplayName");
            return View();
        }

        // ============================================================
        // 3. TẠO ĐƠN (CREATE - POST) -> SỬA LỖI Ở ĐÂY
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequest model)
        {
            // --- SỬA LỖI QUAN TRỌNG ---
            // Loại bỏ validation cho các object liên kết (vì form chỉ gửi ID)
            ModelState.Remove("Student");
            ModelState.Remove("Class");
            // --------------------------

            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            int stuId = int.Parse(stuIdClaim);

            model.StudentId = stuId;
            model.Status = LeaveStatus.Pending;
            model.CreatedAt = DateTime.Now;

            if (model.LeaveDate < DateTime.Today)
            {
                ModelState.AddModelError("LeaveDate", "Không thể xin nghỉ cho ngày trong quá khứ.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi đơn xin nghỉ thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Nếu lỗi -> Load lại dropdown để không bị trắng trang
            var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.Id == stuId);
            var enrolledClasses = await _context.Enrollments.Include(e => e.Class).ThenInclude(c => c.Lecturer)
                .Where(e => e.StudentId == stuId && e.Status == EnrollmentStatus.Approved).Select(e => e.Class).ToListAsync();

            var combinedClasses = new List<Class>();
            if (student.Class != null) combinedClasses.Add(student.Class);
            combinedClasses.AddRange(enrolledClasses);
            combinedClasses = combinedClasses.DistinctBy(c => c.Id).ToList();

            var selectList = combinedClasses.Select(c => new
            {
                Id = c.Id,
                DisplayName = $"{c.ClassName} (GV: {c.Lecturer?.FullName ?? "N/A"})"
            });
            ViewBag.ClassList = new SelectList(selectList, "Id", "DisplayName", model.ClassId);

            return View(model);
        }

        // ============================================================
        // 4. DUYỆT ĐƠN (RESPOND)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Lecturer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Respond(int id, LeaveStatus status, string responseNote)
        {
            var req = await _context.LeaveRequests.Include(l => l.Class).FirstOrDefaultAsync(l => l.Id == id);
            if (req == null) return NotFound();

            req.Status = status;
            req.ResponseNote = responseNote;

            var userAccount = await _context.AppUsers.FirstOrDefaultAsync(u => u.StudentId == req.StudentId);
            if (userAccount != null)
            {
                string statusText = status == LeaveStatus.Approved ? "được CHẤP NHẬN" : "bị TỪ CHỐI";
                string icon = status == LeaveStatus.Approved ? "✅" : "❌";

                _context.Notifications.Add(new Notification
                {
                    UserId = userAccount.Id,
                    Message = $"{icon} Đơn xin nghỉ lớp {req.Class.ClassName} ngày {req.LeaveDate:dd/MM} của bạn đã {statusText}. Ghi chú: {responseNote ?? "Không có"}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xử lý đơn xin nghỉ.";
            return RedirectToAction(nameof(Index));
        }
    }
}