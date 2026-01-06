using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ClassesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClassesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. INDEX (CÓ SEARCH + FILTER)
        // ============================================================
        public async Task<IActionResult> Index(string searchString, int? lecturerId)
        {
            // Query ban đầu (chưa thực thi)
            var classes = _context.Classes.Include(c => c.Lecturer).AsQueryable();

            // 1. Tìm kiếm theo tên lớp hoặc niên khóa
            if (!string.IsNullOrEmpty(searchString))
            {
                classes = classes.Where(c => c.ClassName.Contains(searchString)
                                          || c.AcademicYear.Contains(searchString));
            }

            // 2. Lọc theo Giảng viên
            if (lecturerId.HasValue)
            {
                classes = classes.Where(c => c.LecturerId == lecturerId);
            }

            // Đổ dữ liệu vào View để giữ trạng thái filter
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentLecturer"] = lecturerId;
            ViewData["LecturerList"] = new SelectList(_context.Lecturers, "Id", "FullName");

            return View(await classes.ToListAsync());
        }

        // ============================================================
        // 2. DETAILS
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lopHoc = await _context.Classes
                .Include(c => c.Lecturer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lopHoc == null) return NotFound();

            return View(lopHoc);
        }

        // ============================================================
        // 3. CREATE
        // ============================================================
        public IActionResult Create()
        {
            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Class lopHoc)
        {
            ModelState.Remove("Lecturer"); // Bỏ qua validate object navigation

            if (ModelState.IsValid)
            {
                _context.Add(lopHoc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã tạo lớp học mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName", lopHoc.LecturerId);
            return View(lopHoc);
        }

        // ============================================================
        // 4. EDIT
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lopHoc = await _context.Classes.FindAsync(id);
            if (lopHoc == null) return NotFound();

            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName", lopHoc.LecturerId);
            return View(lopHoc);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Class lopHoc)
        {
            if (id != lopHoc.Id) return NotFound();

            ModelState.Remove("Lecturer");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(lopHoc);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thông tin lớp thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Classes.Any(e => e.Id == lopHoc.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LecturerId"] = new SelectList(_context.Lecturers, "Id", "FullName", lopHoc.LecturerId);
            return View(lopHoc);
        }

        // ============================================================
        // 5. DELETE
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var lopHoc = await _context.Classes
                .Include(c => c.Lecturer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lopHoc == null) return NotFound();

            return View(lopHoc);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lopHoc = await _context.Classes.FindAsync(id);
            if (lopHoc != null)
            {
                // Có thể thêm logic kiểm tra ràng buộc (Sinh viên/Lịch học) tại đây
                _context.Classes.Remove(lopHoc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa lớp học.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}