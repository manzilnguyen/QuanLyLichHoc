using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class EvaluationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EvaluationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. ADMIN: QUẢN LÝ TẤT CẢ ĐÁNH GIÁ (MỚI)
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.LecturerEvaluations
                .Include(e => e.Student)
                .Include(e => e.Lecturer)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            return View(reviews);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.LecturerEvaluations.FindAsync(id);
            if (review != null)
            {
                _context.LecturerEvaluations.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa đánh giá.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 2. SINH VIÊN: VIẾT ĐÁNH GIÁ
        // ============================================================
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Create()
        {
            var username = User.Identity.Name;
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == username);
            if (student == null) return NotFound();

            var lecturers = await _context.Schedules
                .Where(s => s.ClassId == student.ClassId)
                .Select(s => s.Lecturer)
                .Distinct()
                .ToListAsync();

            ViewData["LecturerId"] = new SelectList(lecturers, "Id", "FullName");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LecturerEvaluation model)
        {
            var username = User.Identity.Name;
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == username);

            if (ModelState.IsValid && student != null)
            {
                bool hasRated = await _context.LecturerEvaluations.AnyAsync(e => e.StudentId == student.Id && e.LecturerId == model.LecturerId);
                if (hasRated)
                {
                    TempData["Error"] = "Bạn đã đánh giá giảng viên này rồi.";
                }
                else
                {
                    model.StudentId = student.Id;
                    model.CreatedAt = DateTime.Now;
                    _context.Add(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Gửi đánh giá thành công!";
                }
                return RedirectToAction("MySchedule", "Public");
            }

            var lecturers = await _context.Schedules.Where(s => s.ClassId == student.ClassId).Select(s => s.Lecturer).Distinct().ToListAsync();
            ViewData["LecturerId"] = new SelectList(lecturers, "Id", "FullName", model.LecturerId);
            return View(model);
        }

        // ============================================================
        // 3. GIẢNG VIÊN: XEM ĐÁNH GIÁ VỀ MÌNH
        // ============================================================
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> MyReviews()
        {
            var lecturerIdStr = User.FindFirst("LecturerId")?.Value;
            if (lecturerIdStr == null) return NotFound();
            int lecturerId = int.Parse(lecturerIdStr);

            var reviews = await _context.LecturerEvaluations
                .Include(e => e.Student)
                .Where(e => e.LecturerId == lecturerId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            ViewBag.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;
            ViewBag.TotalReviews = reviews.Count;

            return View(reviews);
        }
    }
}