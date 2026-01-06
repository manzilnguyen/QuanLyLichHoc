using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR; // [MỚI] Dùng cho SignalR
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Hubs; // [MỚI] Import Hubs
using QuanLyLichHoc.Models;
using ClosedXML.Excel;
using System.Data;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class GradesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notiHub; // [MỚI] Inject NotificationHub

        public GradesController(ApplicationDbContext context, IHubContext<NotificationHub> notiHub)
        {
            _context = context;
            _notiHub = notiHub;
        }

        // ============================================================
        // 1. DANH SÁCH (INDEX)
        // ============================================================
        public async Task<IActionResult> Index(string searchString, int? classId, int? subjectId)
        {
            var query = _context.Grades
                .Include(g => g.Student).ThenInclude(s => s.Class)
                .Include(g => g.Subject)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(g => g.Student.FullName.Contains(searchString) || g.Student.StudentCode.Contains(searchString));
            }
            if (classId.HasValue) query = query.Where(g => g.Student.ClassId == classId);
            if (subjectId.HasValue) query = query.Where(g => g.SubjectId == subjectId);

            var data = await query.OrderByDescending(g => g.Id).ToListAsync();

            if (data.Any())
            {
                ViewBag.AvgScore = data.Average(g => g.Score).ToString("0.0");
                ViewBag.PassRate = (data.Count(g => g.Score >= 4.0) * 100.0 / data.Count).ToString("0.0");
                ViewBag.Highest = data.Max(g => g.Score);
            }

            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", classId);
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "Id", "SubjectName", subjectId);
            ViewData["CurrentFilter"] = searchString;

            return View(data);
        }

        // ============================================================
        // 2. NHẬP ĐIỂM + BẮN THÔNG BÁO TỰ ĐỘNG
        // ============================================================
        public IActionResult Create()
        {
            ViewData["ClassList"] = new SelectList(_context.Classes, "Id", "ClassName");
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "Id", "SubjectName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Grade grade)
        {
            if (grade.Score < 0 || grade.Score > 10) ModelState.AddModelError("Score", "Điểm từ 0 đến 10.");
            ModelState.Remove("Student");
            ModelState.Remove("Subject");

            if (ModelState.IsValid)
            {
                // 1. Lưu điểm vào DB
                var exists = await _context.Grades.FirstOrDefaultAsync(g =>
                    g.StudentId == grade.StudentId && g.SubjectId == grade.SubjectId && g.Semester == grade.Semester);

                if (exists != null)
                {
                    exists.Score = grade.Score;
                    _context.Update(exists);
                    TempData["Success"] = "Đã cập nhật điểm sinh viên này.";
                }
                else
                {
                    _context.Add(grade);
                    TempData["Success"] = "Nhập điểm thành công.";
                }
                await _context.SaveChangesAsync();

                // 2. [MỚI] Gửi Thông báo Real-time cho Học sinh & Phụ huynh
                var studentInfo = await _context.Students.Include(s => s.AppUser).FirstOrDefaultAsync(s => s.Id == grade.StudentId);
                var subjectInfo = await _context.Subjects.FindAsync(grade.SubjectId);

                if (studentInfo != null && subjectInfo != null)
                {
                    string msg = $"Môn {subjectInfo.SubjectName} ({grade.Semester}): {grade.Score} điểm";
                    string url = "/Public/MyGrades"; // Link bấm vào để xem điểm

                    // A. Gửi cho Học sinh (nếu có tài khoản)
                    if (studentInfo.AppUser != null)
                    {
                        await _notiHub.Clients.User(studentInfo.AppUser.Username).SendAsync("ReceiveNotification", "📢 Điểm số mới", msg, url, "Success");
                    }

                    // B. Gửi cho Phụ huynh (Quy tắc Username: MãSV + PH)
                    string parentUsername = studentInfo.StudentCode + "PH";
                    await _notiHub.Clients.User(parentUsername).SendAsync("ReceiveNotification", "📢 Kết quả học tập của con", msg, url, "Success");
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["ClassList"] = new SelectList(_context.Classes, "Id", "ClassName");
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "Id", "SubjectName", grade.SubjectId);
            return View(grade);
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentsByClass(int classId)
        {
            var students = await _context.Students
                .Where(s => s.ClassId == classId)
                .Select(s => new { id = s.Id, name = $"{s.StudentCode} - {s.FullName}" })
                .ToListAsync();
            return Json(students);
        }

        // ============================================================
        // 3. IMPORT EXCEL
        // ============================================================
        public IActionResult Import() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0) { TempData["Error"] = "Vui lòng chọn file."; return View(); }

            int count = 0;
            List<string> errors = new List<string>();
            var students = await _context.Students.ToListAsync();
            var subjects = await _context.Subjects.ToListAsync();

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
                            string stuCode = row.Cell(1).GetValue<string>().Trim();
                            string subCode = row.Cell(2).GetValue<string>().Trim();
                            double score = row.Cell(3).GetValue<double>();
                            string semester = row.Cell(4).GetValue<string>().Trim();

                            var stu = students.FirstOrDefault(s => s.StudentCode == stuCode);
                            var sub = subjects.FirstOrDefault(s => s.SubjectCode == subCode);

                            if (stu == null || sub == null) { errors.Add($"Dòng {row.RowNumber()}: Mã SV/Môn sai."); continue; }

                            var exist = await _context.Grades.FirstOrDefaultAsync(g => g.StudentId == stu.Id && g.SubjectId == sub.Id && g.Semester == semester);
                            if (exist != null) { exist.Score = score; _context.Update(exist); }
                            else { _context.Add(new Grade { StudentId = stu.Id, SubjectId = sub.Id, Score = score, Semester = semester }); }
                            count++;
                        }
                        catch (Exception ex) { errors.Add($"Dòng {row.RowNumber()}: {ex.Message}"); }
                    }
                    await _context.SaveChangesAsync();
                }
            }
            TempData["Success"] = $"Đã xử lý {count} dòng.";
            if (errors.Any()) TempData["Error"] = string.Join("; ", errors.Take(5));
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4. EXPORT EXCEL
        // ============================================================
        public async Task<IActionResult> Export()
        {
            var data = await _context.Grades.Include(g => g.Student).ThenInclude(s => s.Class).Include(g => g.Subject).OrderBy(g => g.Student.Class.ClassName).ToListAsync();
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("BangDiem");
                ws.Cell(1, 1).Value = "BẢNG ĐIỂM";
                ws.Cell(3, 1).Value = "Mã SV"; ws.Cell(3, 2).Value = "Họ Tên"; ws.Cell(3, 3).Value = "Lớp";
                ws.Cell(3, 4).Value = "Môn"; ws.Cell(3, 5).Value = "Điểm"; ws.Cell(3, 6).Value = "Xếp loại";

                int r = 4;
                foreach (var item in data)
                {
                    ws.Cell(r, 1).Value = item.Student.StudentCode;
                    ws.Cell(r, 2).Value = item.Student.FullName;
                    ws.Cell(r, 3).Value = item.Student.Class?.ClassName;
                    ws.Cell(r, 4).Value = item.Subject.SubjectName;
                    ws.Cell(r, 5).Value = item.Score;
                    ws.Cell(r, 6).Value = item.Score >= 8.5 ? "Giỏi" : (item.Score >= 5 ? "Đạt" : "Trượt");
                    r++;
                }
                using (var stream = new MemoryStream()) { workbook.SaveAs(stream); return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Diem.xlsx"); }
            }
        }
    }
}