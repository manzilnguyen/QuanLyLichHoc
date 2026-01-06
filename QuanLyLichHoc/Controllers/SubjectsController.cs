using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DANH SÁCH (INDEX) - CÓ TÌM KIẾM
        // ============================================================
        public async Task<IActionResult> Index(string searchString)
        {
            // Query cơ bản lấy tất cả môn học
            var subjects = from s in _context.Subjects
                           select s;

            // Nếu có từ khóa tìm kiếm -> Lọc dữ liệu
            if (!String.IsNullOrEmpty(searchString))
            {
                subjects = subjects.Where(s => s.SubjectName.Contains(searchString)
                                            || s.SubjectCode.Contains(searchString));
            }

            // Lưu lại từ khóa tìm kiếm để hiển thị lại trên ô input ở View
            ViewData["CurrentFilter"] = searchString;

            return View(await subjects.ToListAsync());
        }

        // ============================================================
        // 2. CHI TIẾT (DETAILS)
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(m => m.Id == id);

            if (subject == null)
            {
                return NotFound();
            }

            return View(subject);
        }

        // ============================================================
        // 3. TẠO MỚI (CREATE)
        // ============================================================
        // GET: Subjects/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Subjects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SubjectCode,SubjectName,Credits")] Subject subject)
        {
            if (ModelState.IsValid)
            {
                // Kiểm tra trùng mã môn học
                bool isDuplicate = await _context.Subjects.AnyAsync(s => s.SubjectCode == subject.SubjectCode);
                if (isDuplicate)
                {
                    ModelState.AddModelError("SubjectCode", "Mã môn học này đã tồn tại trong hệ thống.");
                    return View(subject);
                }

                _context.Add(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm môn học mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(subject);
        }

        // ============================================================
        // 4. CHỈNH SỬA (EDIT)
        // ============================================================
        // GET: Subjects/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
            {
                return NotFound();
            }
            return View(subject);
        }

        // POST: Subjects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SubjectCode,SubjectName,Credits")] Subject subject)
        {
            if (id != subject.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra trùng mã môn (trừ chính nó ra)
                    bool isDuplicate = await _context.Subjects
                        .AnyAsync(s => s.SubjectCode == subject.SubjectCode && s.Id != id);

                    if (isDuplicate)
                    {
                        ModelState.AddModelError("SubjectCode", "Mã môn học này đã được sử dụng.");
                        return View(subject);
                    }

                    _context.Update(subject);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thông tin thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SubjectExists(subject.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(subject);
        }

        // ============================================================
        // 5. XÓA (DELETE)
        // ============================================================
        // GET: Subjects/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(m => m.Id == id);

            if (subject == null)
            {
                return NotFound();
            }

            return View(subject);
        }

        // POST: Subjects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject != null)
            {
                // Logic kiểm tra ràng buộc trước khi xóa (nếu có) có thể đặt ở đây

                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa môn học thành công.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool SubjectExists(int id)
        {
            return _context.Subjects.Any(e => e.Id == id);
        }
    }
}