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
    public class LecturersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LecturersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DANH SÁCH (SEARCH + FILTER)
        // ============================================================
        public async Task<IActionResult> Index(string searchString, string currentFilter, string departmentFilter)
        {
            var lecturers = from l in _context.Lecturers select l;

            // Lọc theo Khoa
            if (!string.IsNullOrEmpty(departmentFilter))
            {
                lecturers = lecturers.Where(l => l.Department == departmentFilter);
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                lecturers = lecturers.Where(l => l.FullName.Contains(searchString)
                                              || l.Email.Contains(searchString));
            }

            // Dữ liệu cho Dropdown lọc
            var deptList = await _context.Lecturers.Select(l => l.Department).Distinct().ToListAsync();
            ViewData["Departments"] = new SelectList(deptList);

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentDept"] = departmentFilter;

            return View(await lecturers.ToListAsync());
        }

        // ============================================================
        // 2. EXPORT EXCEL (MỚI THÊM)
        // ============================================================
        public async Task<IActionResult> Export()
        {
            var lecturers = await _context.Lecturers.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("DanhSachGiangVien");

                // --- 1. Tạo Tiêu đề (Header) ---
                worksheet.Cell(1, 1).Value = "STT";
                worksheet.Cell(1, 2).Value = "Họ và Tên";
                worksheet.Cell(1, 3).Value = "Email";
                worksheet.Cell(1, 4).Value = "Số điện thoại";
                worksheet.Cell(1, 5).Value = "Khoa / Bộ môn";

                // Style cho Header (In đậm, nền xám nhẹ)
                var headerRange = worksheet.Range("A1:E1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // --- 2. Đổ dữ liệu ---
                int row = 2;
                for (int i = 0; i < lecturers.Count; i++)
                {
                    var item = lecturers[i];
                    worksheet.Cell(row, 1).Value = i + 1;
                    worksheet.Cell(row, 2).Value = item.FullName;
                    worksheet.Cell(row, 3).Value = item.Email;
                    worksheet.Cell(row, 4).Value = item.PhoneNumber ?? ""; // Xử lý null
                    worksheet.Cell(row, 5).Value = item.Department ?? "";
                    row++;
                }

                // --- 3. Format cột ---
                worksheet.Columns().AdjustToContents(); // Tự động căn chỉnh độ rộng

                // Kẻ khung (Borders) cho toàn bộ bảng
                var dataRange = worksheet.Range(1, 1, row - 1, 5);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // --- 4. Trả về file ---
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DanhSachGiangVien.xlsx");
                }
            }
        }

        // ============================================================
        // 3. IMPORT EXCEL
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
                        var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Bỏ qua header

                        foreach (var row in rows)
                        {
                            try
                            {
                                string name = row.Cell(1).GetValue<string>().Trim();
                                string email = row.Cell(2).GetValue<string>().Trim();
                                string phone = row.Cell(3).GetValue<string>().Trim();
                                string dept = row.Cell(4).GetValue<string>().Trim();

                                if (string.IsNullOrEmpty(email)) continue;

                                if (!_context.Lecturers.Any(l => l.Email == email))
                                {
                                    _context.Add(new Lecturer { FullName = name, Email = email, PhoneNumber = phone, Department = dept });
                                    count++;
                                }
                            }
                            catch (Exception ex) { errors.Add($"Dòng {row.RowNumber()}: {ex.Message}"); }
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                if (count > 0) TempData["Success"] = $"Đã import {count} giảng viên!";
                else TempData["Warning"] = "Không có dữ liệu mới.";

                if (errors.Any()) TempData["Error"] = $"Lỗi: {string.Join("; ", errors.Take(3))}";
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi file: " + ex.Message; }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4. CRUD CƠ BẢN (Create, Edit, Delete)
        // ============================================================

        // --- Create ---
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lecturer lecturer)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Lecturers.AnyAsync(l => l.Email == lecturer.Email))
                {
                    ModelState.AddModelError("Email", "Email đã tồn tại.");
                    return View(lecturer);
                }
                _context.Add(lecturer);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(lecturer);
        }

        // --- Edit ---
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer == null) return NotFound();
            return View(lecturer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lecturer lecturer)
        {
            if (id != lecturer.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try { _context.Update(lecturer); await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { if (!_context.Lecturers.Any(e => e.Id == lecturer.Id)) return NotFound(); else throw; }
                TempData["Success"] = "Cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(lecturer);
        }

        // --- Delete ---
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var l = await _context.Lecturers.FirstOrDefaultAsync(m => m.Id == id);
            if (l == null) return NotFound();
            return View(l);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer != null)
            {
                var acc = await _context.AppUsers.FirstOrDefaultAsync(u => u.LecturerId == id);
                if (acc != null) _context.AppUsers.Remove(acc);
                _context.Lecturers.Remove(lecturer);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa giảng viên.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}