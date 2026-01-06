using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using ClosedXML.Excel; // Cần cài NuGet: ClosedXML
using System.IO;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DANH SÁCH (CÓ SEARCH + FILTER)
        // ============================================================
        public async Task<IActionResult> Index(string searchString, int? filterClassId)
        {
            var query = _context.Students.Include(s => s.Class).AsQueryable();

            // Lọc theo lớp
            if (filterClassId.HasValue)
            {
                query = query.Where(s => s.ClassId == filterClassId);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.FullName.Contains(searchString)
                                      || s.StudentCode.Contains(searchString));
            }

            // Dữ liệu cho View
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentClass"] = filterClassId;
            ViewData["ClassList"] = new SelectList(_context.Classes, "Id", "ClassName");

            return View(await query.OrderByDescending(s => s.Id).ToListAsync());
        }

        // ============================================================
        // 2. EXPORT EXCEL (MỚI)
        // ============================================================
        public async Task<IActionResult> Export()
        {
            var students = await _context.Students.Include(s => s.Class).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("DanhSachHocSinh");

                // Header
                worksheet.Cell(1, 1).Value = "STT";
                worksheet.Cell(1, 2).Value = "Mã Sinh Viên";
                worksheet.Cell(1, 3).Value = "Họ và Tên";
                worksheet.Cell(1, 4).Value = "Ngày Sinh";
                worksheet.Cell(1, 5).Value = "Lớp";
                worksheet.Cell(1, 6).Value = "Số điện thoại";

                // Style Header
                var headerRange = worksheet.Range("A1:F1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Data
                int row = 2;
                for (int i = 0; i < students.Count; i++)
                {
                    var item = students[i];
                    worksheet.Cell(row, 1).Value = i + 1;
                    worksheet.Cell(row, 2).Value = item.StudentCode;
                    worksheet.Cell(row, 3).Value = item.FullName;
                    worksheet.Cell(row, 4).Value = item.DateOfBirth.ToString("dd/MM/yyyy");
                    worksheet.Cell(row, 5).Value = item.Class?.ClassName ?? "Chưa phân lớp";
                    worksheet.Cell(row, 6).Value = item.PhoneNumber;
                    row++;
                }

                // AutoFit & Border
                worksheet.Columns().AdjustToContents();
                worksheet.Range(1, 1, row - 1, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                worksheet.Range(1, 1, row - 1, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DanhSachHocSinh.xlsx");
                }
            }
        }

        // ============================================================
        // 3. IMPORT EXCEL (THÔNG MINH)
        // ============================================================
        public IActionResult Import() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel.";
                return View();
            }

            int count = 0;
            var errors = new List<string>();

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheets.First();
                        var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                        // Lấy danh sách lớp để check nhanh
                        var allClasses = await _context.Classes.ToListAsync();

                        foreach (var row in rows)
                        {
                            try
                            {
                                string code = row.Cell(1).GetValue<string>().Trim();
                                string name = row.Cell(2).GetValue<string>().Trim();
                                if (!row.Cell(3).TryGetValue(out DateTime dob)) dob = DateTime.Now;
                                string className = row.Cell(4).GetValue<string>().Trim();
                                string phone = row.Cell(5).GetValue<string>().Trim();

                                if (string.IsNullOrEmpty(code)) continue;

                                // 1. Xử lý Lớp: Nếu chưa có tên lớp này -> Tạo mới tự động
                                var targetClass = allClasses.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
                                if (targetClass == null && !string.IsNullOrEmpty(className))
                                {
                                    targetClass = new Class { ClassName = className, AcademicYear = DateTime.Now.Year.ToString() };
                                    _context.Classes.Add(targetClass);
                                    await _context.SaveChangesAsync();
                                    allClasses.Add(targetClass); // Update cache
                                }

                                // 2. Thêm Học sinh (nếu chưa trùng Mã SV)
                                if (!_context.Students.Any(s => s.StudentCode == code))
                                {
                                    var st = new Student
                                    {
                                        StudentCode = code,
                                        FullName = name,
                                        DateOfBirth = dob,
                                        ClassId = targetClass?.Id ?? 0,
                                        PhoneNumber = phone
                                    };

                                    // Nếu ko có lớp thì gán tạm lớp đầu tiên hoặc bỏ qua validate class nếu cần
                                    if (targetClass == null)
                                    {
                                        // Logic phụ: Nếu excel ko ghi lớp, có thể gán lớp mặc định hoặc báo lỗi. 
                                        // Ở đây giả sử bắt buộc có lớp -> nếu targetClass null thì lỗi validate SQL
                                        // Ta có thể skip hoặc gán ID=1.
                                        continue;
                                    }

                                    _context.Add(st);

                                    // 3. Tự tạo User (Username=MãSV, Pass=123456)
                                    if (!_context.AppUsers.Any(u => u.Username == code))
                                    {
                                        _context.AppUsers.Add(new AppUser { Username = code, Password = "123456", Role = "Student", Student = st });
                                    }
                                    count++;
                                }
                            }
                            catch (Exception ex) { errors.Add($"Dòng {row.RowNumber()}: {ex.Message}"); }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                TempData["Success"] = $"Đã import thành công {count} học sinh!";
                if (errors.Any()) TempData["Error"] = "Có lỗi: " + string.Join("; ", errors.Take(3));
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi file: " + ex.Message; }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4. CREATE (Tự tạo Account)
        // ============================================================
        public IActionResult Create()
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Student student)
        {
            ModelState.Remove("Class"); // Bỏ qua validate relationship
            if (ModelState.IsValid)
            {
                if (await _context.Students.AnyAsync(s => s.StudentCode == student.StudentCode))
                {
                    ModelState.AddModelError("StudentCode", "Mã sinh viên đã tồn tại.");
                }
                else
                {
                    _context.Add(student);
                    // Tạo Account
                    _context.Add(new AppUser
                    {
                        Username = student.StudentCode,
                        Password = "123456",
                        Role = "Student",
                        Student = student
                    });

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Thêm mới thành công (Đã tạo tài khoản mặc định).";
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", student.ClassId);
            return View(student);
        }

        // ============================================================
        // 5. EDIT
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", student.ClassId);
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Student student)
        {
            if (id != student.Id) return NotFound();
            ModelState.Remove("Class");

            if (ModelState.IsValid)
            {
                try { _context.Update(student); await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Students.Any(e => e.Id == student.Id)) return NotFound(); else throw;
                }
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", student.ClassId);
            return View(student);
        }

        // ============================================================
        // 6. DELETE (Xóa sạch dữ liệu liên quan)
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(m => m.Id == id);
            if (student == null) return NotFound();
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                // Xóa các bảng phụ thuộc (Điểm danh, Điểm số, User...)
                var atts = _context.Attendances.Where(a => a.StudentId == id); _context.Attendances.RemoveRange(atts);
                var grades = _context.Grades.Where(g => g.StudentId == id); _context.Grades.RemoveRange(grades);
                var users = _context.AppUsers.Where(u => u.StudentId == id); _context.AppUsers.RemoveRange(users);

                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa học sinh và dữ liệu liên quan.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 7. Details
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(m => m.Id == id);
            if (student == null) return NotFound();
            return View(student);
        }

        // ============================================================
        // 8. Auto Create Account Button Action
        // ============================================================
        public async Task<IActionResult> AutoCreateAccounts()
        {
            var students = await _context.Students.ToListAsync();
            int count = 0;
            foreach (var s in students)
            {
                if (!_context.AppUsers.Any(u => u.Username == s.StudentCode))
                {
                    _context.AppUsers.Add(new AppUser { Username = s.StudentCode, Password = "123456", Role = "Student", StudentId = s.Id });
                    count++;
                }
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo bổ sung {count} tài khoản.";
            return RedirectToAction(nameof(Index));
        }
    }
}