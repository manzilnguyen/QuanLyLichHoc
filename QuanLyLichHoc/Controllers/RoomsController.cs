using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. INDEX (TÌM KIẾM + LỌC)
        // ============================================================
        public async Task<IActionResult> Index(string searchString, RoomType? typeFilter)
        {
            var rooms = from r in _context.Rooms select r;

            // 1. Tìm kiếm theo tên phòng
            if (!string.IsNullOrEmpty(searchString))
            {
                rooms = rooms.Where(s => s.RoomName.Contains(searchString));
            }

            // 2. Lọc theo loại phòng (nếu có chọn)
            if (typeFilter.HasValue)
            {
                rooms = rooms.Where(s => s.Type == typeFilter);
            }

            // Lưu trạng thái để hiển thị lại trên View
            ViewData["CurrentFilter"] = searchString;
            ViewData["TypeFilter"] = typeFilter;

            return View(await rooms.ToListAsync());
        }

        // ============================================================
        // 2. CREATE
        // ============================================================
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Room room)
        {
            if (ModelState.IsValid)
            {
                _context.Add(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm phòng học mới!";
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // ============================================================
        // 3. EDIT
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();
            return View(room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Room room)
        {
            if (id != room.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thông tin phòng thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Rooms.Any(e => e.Id == room.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(room);
        }

        // ============================================================
        // 4. DELETE
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var room = await _context.Rooms.FirstOrDefaultAsync(m => m.Id == id);
            if (room == null) return NotFound();
            return View(room);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room != null)
            {
                // Kiểm tra ràng buộc nếu cần (ví dụ: phòng đang có lịch học)
                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa phòng học.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}