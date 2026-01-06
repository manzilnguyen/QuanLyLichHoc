using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using ClosedXML.Excel; // Cần cài NuGet: ClosedXML
using System.IO;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EnrollmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DÀNH CHO HỌC SINH: ĐĂNG KÝ (THÔNG MINH HƠN)
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> RegisterIndex(string searchString)
        {
            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            int stuId = stuIdClaim != null ? int.Parse(stuIdClaim) : 0;

            // Lấy danh sách Enrollments của SV này để check trạng thái
            var myEnrollments = await _context.Enrollments
                .Where(e => e.StudentId == stuId && e.Status == EnrollmentStatus.Pending)
                .Select(e => e.ClassId)
                .ToListAsync();

            var currentClassId = await _context.Students
                .Where(s => s.Id == stuId)
                .Select(s => s.ClassId)
                .FirstOrDefaultAsync();

            // Query Lớp học
            var query = _context.Classes.Include(c => c.Lecturer).AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c => c.ClassName.Contains(searchString) || c.Lecturer.FullName.Contains(searchString));
            }

            var classes = await query.Select(c => new ClassViewModel
            {
                Id = c.Id,
                ClassName = c.ClassName,
                AcademicYear = c.AcademicYear,
                LecturerName = c.Lecturer != null ? c.Lecturer.FullName : "Chưa phân công",
                MaxQuantity = c.MaxQuantity,
                CurrentQuantity = _context.Students.Count(s => s.ClassId == c.Id),

                // Logic thông minh: Xác định trạng thái của User với lớp này
                UserStatus = c.Id == currentClassId ? "Joined" :
                             (myEnrollments.Contains(c.Id) ? "Pending" : "None")
            }).ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            return View(classes);
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int classId)
        {
            var stuIdClaim = User.FindFirst("StudentId")?.Value;
            if (stuIdClaim == null) return RedirectToAction("Login", "Account");
            int stuId = int.Parse(stuIdClaim);

            // Kiểm tra kỹ lại 1 lần nữa ở Server side
            var lop = await _context.Classes.FindAsync(classId);
            int currentQty = await _context.Students.CountAsync(s => s.ClassId == classId);

            if (currentQty >= lop.MaxQuantity)
            {
                TempData["Error"] = "Lớp đã đầy ngay trong lúc bạn đang thao tác.";
                return RedirectToAction("RegisterIndex");
            }

            if (!_context.Enrollments.Any(e => e.StudentId == stuId && e.ClassId == classId && e.Status == EnrollmentStatus.Pending))
            {
                _context.Add(new Enrollment { StudentId = stuId, ClassId = classId, RegisterDate = DateTime.Now, Status = EnrollmentStatus.Pending });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã gửi yêu cầu tham gia!";
            }
            return RedirectToAction("RegisterIndex");
        }

        // ============================================================
        // 2. ADMIN: DUYỆT (CÓ LỌC & DUYỆT HÀNG LOẠT)
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PendingList(string searchString)
        {
            var query = _context.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Class)
                .Where(e => e.Status == EnrollmentStatus.Pending)
                .OrderByDescending(e => e.RegisterDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(e => e.Student.FullName.Contains(searchString) || e.Class.ClassName.Contains(searchString));
            }

            ViewData["CurrentFilter"] = searchString;
            return View(await query.ToListAsync());
        }

        // Xử lý duyệt đơn lẻ (Giữ nguyên logic cũ nhưng clean hơn)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, bool isApproved)
        {
            await ProcessApproval(id, isApproved);
            TempData["Success"] = isApproved ? "Đã duyệt." : "Đã từ chối.";
            return RedirectToAction("PendingList");
        }

        // Xử lý duyệt hàng loạt (MỚI)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> BulkApprove(List<int> ids, bool isApproved)
        {
            if (ids == null || !ids.Any())
            {
                TempData["Error"] = "Chưa chọn yêu cầu nào.";
                return RedirectToAction("PendingList");
            }

            int count = 0;
            foreach (var id in ids)
            {
                await ProcessApproval(id, isApproved);
                count++;
            }

            TempData["Success"] = $"Đã xử lý {count} yêu cầu.";
            return RedirectToAction("PendingList");
        }

        // Hàm xử lý logic chung (Private)
        private async Task ProcessApproval(int enrollmentId, bool isApproved)
        {
            var enrollment = await _context.Enrollments.Include(e => e.Class).FirstOrDefaultAsync(e => e.Id == enrollmentId);
            if (enrollment == null) return;

            var userAccount = await _context.AppUsers.FirstOrDefaultAsync(u => u.StudentId == enrollment.StudentId);

            if (isApproved)
            {
                int currentQty = await _context.Students.CountAsync(s => s.ClassId == enrollment.ClassId);
                if (currentQty < enrollment.Class.MaxQuantity)
                {
                    enrollment.Status = EnrollmentStatus.Approved;
                    var student = await _context.Students.FindAsync(enrollment.StudentId);
                    if (student != null) student.ClassId = enrollment.ClassId;

                    if (userAccount != null) _context.Notifications.Add(new Notification { UserId = userAccount.Id, Message = $"✅ Yêu cầu vào lớp {enrollment.Class.ClassName} đã được duyệt.", CreatedAt = DateTime.Now });
                }
            }
            else
            {
                enrollment.Status = EnrollmentStatus.Rejected;
                if (userAccount != null) _context.Notifications.Add(new Notification { UserId = userAccount.Id, Message = $"❌ Yêu cầu vào lớp {enrollment.Class.ClassName} bị từ chối.", CreatedAt = DateTime.Now });
            }
            await _context.SaveChangesAsync();
        }

        // ============================================================
        // 3. GIẢNG VIÊN: QUẢN LÝ LỚP + XUẤT EXCEL
        // ============================================================
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> MyClass()
        {
            var lecIdClaim = User.FindFirst("LecturerId")?.Value;
            if (lecIdClaim == null) return RedirectToAction("Login", "Account");
            int lecId = int.Parse(lecIdClaim);

            var myClasses = await _context.Classes.Where(c => c.LecturerId == lecId).ToListAsync();

            ViewBag.Students = await _context.Students
                .Include(s => s.Class)
                .Where(s => s.Class.LecturerId == lecId)
                .OrderBy(s => s.ClassId).ThenBy(s => s.StudentCode)
                .ToListAsync();

            return View(myClasses);
        }

        // Tính năng mới: Xuất danh sách sinh viên của lớp ra Excel
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> ExportClassList(int classId)
        {
            var lop = await _context.Classes.FindAsync(classId);
            if (lop == null) return NotFound();

            var students = await _context.Students.Where(s => s.ClassId == classId).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("DanhSachLop");

                // Header
                worksheet.Cell(1, 1).Value = $"DANH SÁCH LỚP: {lop.ClassName} ({lop.AcademicYear})";
                worksheet.Range("A1:D1").Merge().Style.Font.Bold = true;

                worksheet.Cell(3, 1).Value = "STT";
                worksheet.Cell(3, 2).Value = "Mã SV";
                worksheet.Cell(3, 3).Value = "Họ và Tên";
                worksheet.Cell(3, 4).Value = "Số điện thoại";
                worksheet.Range("A3:D3").Style.Font.Bold = true;
                worksheet.Range("A3:D3").Style.Fill.BackgroundColor = XLColor.LightGray;

                int row = 4;
                for (int i = 0; i < students.Count; i++)
                {
                    worksheet.Cell(row, 1).Value = i + 1;
                    worksheet.Cell(row, 2).Value = students[i].StudentCode;
                    worksheet.Cell(row, 3).Value = students[i].FullName;
                    worksheet.Cell(row, 4).Value = students[i].PhoneNumber;
                    row++;
                }
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"DanhSach_{lop.ClassName}.xlsx");
                }
            }
        }
    }

    public class ClassViewModel
    {
        public int Id { get; set; }
        public string ClassName { get; set; }
        public string AcademicYear { get; set; }
        public string LecturerName { get; set; }
        public int CurrentQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public string UserStatus { get; set; } // "Joined", "Pending", "None"
    }
}