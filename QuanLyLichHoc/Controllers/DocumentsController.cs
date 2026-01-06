using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using Microsoft.AspNetCore.Hosting; // Cần để xử lý file
using System.IO;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ============================================================
        // 1. DÀNH CHO HỌC SINH: TỰ ĐỘNG TÌM TÀI LIỆU LỚP MÌNH
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyDocs()
        {
            var username = User.Identity.Name;

            // Tìm thông tin học sinh qua tài khoản đăng nhập
            var user = await _context.AppUsers
                .Include(u => u.Student)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user?.Student != null && user.Student.ClassId != 0)
            {
                // Chuyển hướng đến trang danh sách tài liệu của lớp đó
                return RedirectToAction("Index", new { classId = user.Student.ClassId });
            }

            return Content("Bạn chưa được phân vào lớp học nào hoặc chưa liên kết hồ sơ.");
        }

        // ============================================================
        // 2. XEM DANH SÁCH TÀI LIỆU (CHUNG)
        // ============================================================
        public async Task<IActionResult> Index(int classId)
        {
            var documents = await _context.Documents
                .Include(d => d.Class)
                .Where(d => d.ClassId == classId)
                .OrderByDescending(d => d.UploadDate)
                .ToListAsync();

            var lop = await _context.Classes.FindAsync(classId);
            ViewBag.ClassName = lop?.ClassName ?? "Lớp học";
            ViewBag.ClassId = classId;

            return View(documents);
        }

        // ============================================================
        // 3. UPLOAD FILE (CHỈ GIẢNG VIÊN / ADMIN)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int classId, string fileName, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                // 1. Tạo thư mục lưu trữ nếu chưa có
                string uploadPath = Path.Combine(_env.WebRootPath, "documents");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                // 2. Tạo tên file duy nhất
                string uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
                string filePath = Path.Combine(uploadPath, uniqueName);

                // 3. Lưu file vật lý
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 4. Lưu vào Database
                // Lấy ID người upload (nếu là GV)
                int uploaderId = 0;
                var userClaim = User.FindFirst("LecturerId")?.Value;
                if (userClaim != null) uploaderId = int.Parse(userClaim);

                var doc = new Document
                {
                    FileName = string.IsNullOrEmpty(fileName) ? file.FileName : fileName,
                    FilePath = "/documents/" + uniqueName,
                    UploadDate = DateTime.Now,
                    ClassId = classId,
                    UploaderId = uploaderId
                };

                _context.Add(doc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã tải lên tài liệu thành công!";
            }
            else
            {
                TempData["Error"] = "Vui lòng chọn file.";
            }

            return RedirectToAction("Index", new { classId = classId });
        }

        // ============================================================
        // 4. XÓA FILE (CHỈ GIẢNG VIÊN / ADMIN)
        // ============================================================
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc != null)
            {
                // Xóa file vật lý (Optional - để tiết kiệm dung lượng)
                try
                {
                    string path = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                catch { }

                _context.Documents.Remove(doc);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài liệu.";

                return RedirectToAction("Index", new { classId = doc.ClassId });
            }
            return NotFound();
        }
    }
}