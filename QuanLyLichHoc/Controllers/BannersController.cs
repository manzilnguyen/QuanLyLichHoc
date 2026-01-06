using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BannersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public BannersController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 1. DANH SÁCH
        public async Task<IActionResult> Index()
        {
            // Sắp xếp theo Thứ tự ưu tiên -> Ngày tạo mới nhất
            return View(await _context.Banners
                .OrderByDescending(b => b.Priority)
                .ThenByDescending(b => b.CreatedAt)
                .ToListAsync());
        }

        // 2. TẠO MỚI
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Banner banner, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    banner.ImageUrl = await UploadFile(imageFile);
                }
                else
                {
                    banner.ImageUrl = "https://via.placeholder.com/800x400?text=Event";
                }

                _context.Add(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã thêm banner thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(banner);
        }

        // 3. CHỈNH SỬA (MỚI)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null) return NotFound();
            return View(banner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Banner banner, IFormFile? imageFile)
        {
            if (id != banner.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Nếu có upload ảnh mới
                    if (imageFile != null)
                    {
                        // 1. Xóa ảnh cũ (nếu không phải ảnh mặc định)
                        if (!string.IsNullOrEmpty(banner.ImageUrl) && !banner.ImageUrl.StartsWith("http"))
                        {
                            string oldPath = Path.Combine(_env.WebRootPath, banner.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                        }

                        // 2. Upload ảnh mới
                        banner.ImageUrl = await UploadFile(imageFile);
                    }

                    _context.Update(banner);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật banner thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Banners.Any(e => e.Id == banner.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(banner);
        }

        // 4. XÓA (THÔNG MINH: XÓA CẢ FILE ẢNH)
        public async Task<IActionResult> Delete(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                // Xóa file ảnh khỏi server
                if (!string.IsNullOrEmpty(banner.ImageUrl) && !banner.ImageUrl.StartsWith("http"))
                {
                    string filePath = Path.Combine(_env.WebRootPath, banner.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                }

                _context.Banners.Remove(banner);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa banner.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 5. BẬT/TẮT HIỂN THỊ
        public async Task<IActionResult> ToggleActive(int id)
        {
            var banner = await _context.Banners.FindAsync(id);
            if (banner != null)
            {
                banner.IsActive = !banner.IsActive;
                await _context.SaveChangesAsync();
                TempData["Success"] = banner.IsActive ? "Đã bật hiển thị banner." : "Đã ẩn banner.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- HELPER: UPLOAD FILE ---
        private async Task<string> UploadFile(IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "banners");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return "/banners/" + uniqueName;
        }
    }
}