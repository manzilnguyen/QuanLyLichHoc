using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace QuanLyLichHoc.Controllers
{
    [Authorize] // Yêu cầu đăng nhập mới truy cập được
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. TRANG CHỦ DASHBOARD
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // 1. LẤY DỮ LIỆU CHUNG (Banner & Lời chào)
            ViewBag.Banners = await GetActiveBanners();

            var hour = DateTime.Now.Hour;
            ViewBag.Greeting = hour < 12 ? "Chào buổi sáng" : (hour < 18 ? "Chào buổi chiều" : "Chào buổi tối");

            // 2. LOGIC DÀNH CHO ADMIN / MANAGER (Thống kê hệ thống)
            if (User.IsInRole("Admin") || User.IsInRole("Manager"))
            {
                // Thống kê số lượng
                ViewBag.TotalStudents = await _context.Students.CountAsync();
                ViewBag.TotalLecturers = await _context.Lecturers.CountAsync();
                ViewBag.TotalClasses = await _context.Classes.CountAsync();
                ViewBag.TotalCourses = await _context.Subjects.CountAsync();

                // Dữ liệu biểu đồ (Top 10 lớp đông nhất)
                var classStats = await _context.Classes
                    .Select(c => new {
                        Name = c.ClassName,
                        Count = _context.Students.Count(s => s.ClassId == c.Id)
                    })
                    .Where(x => x.Count > 0)
                    .OrderByDescending(x => x.Count)
                    .Take(10)
                    .ToListAsync();

                ViewBag.ChartLabels = classStats.Select(x => x.Name).ToArray();
                ViewBag.ChartData = classStats.Select(x => x.Count).ToArray();

                return View(); // Trả về View Dashboard Admin
            }

            // 3. LOGIC DÀNH CHO HỌC SINH & GIẢNG VIÊN (Smart Dashboard)
            if (User.IsInRole("Student") || User.IsInRole("Lecturer"))
            {
                // Tìm LỚP HỌC SẮP TỚI hoặc ĐANG DIỄN RA
                Schedule? nextClass = null;
                var now = DateTime.Now.TimeOfDay;
                var today = DateTime.Today.DayOfWeek;

                // Query cơ bản lấy lịch hôm nay và chưa kết thúc
                IQueryable<Schedule> query = _context.Schedules
                    .Include(s => s.Class)
                    .Include(s => s.Subject)
                    .Include(s => s.Room)
                    .Include(s => s.Lecturer)
                    .Where(s => s.DayOfWeek == today && s.EndTime > now)
                    .OrderBy(s => s.StartTime);

                // Lọc theo User
                if (User.IsInRole("Student"))
                {
                    var idClaim = User.FindFirst("ClassId")?.Value;
                    if (idClaim != null)
                    {
                        int classId = int.Parse(idClaim);
                        query = query.Where(s => s.ClassId == classId);
                    }
                }
                else if (User.IsInRole("Lecturer"))
                {
                    var idClaim = User.FindFirst("LecturerId")?.Value;
                    if (idClaim != null)
                    {
                        int lecturerId = int.Parse(idClaim);
                        query = query.Where(s => s.LecturerId == lecturerId);
                    }
                }

                // Lấy lớp gần nhất
                nextClass = await query.FirstOrDefaultAsync();
                ViewBag.NextClass = nextClass; // Truyền sang View để hiển thị thẻ "Sắp diễn ra"

                return View(); // Trả về View Personal Dashboard
            }

            return View();
        }

        // Helper: Lấy Banner đang hoạt động
        private async Task<List<Banner>> GetActiveBanners()
        {
            try
            {
                return await _context.Banners
                    .Where(b => b.IsActive)
                    .OrderBy(b => b.Priority)       // Ưu tiên trước
                    .ThenByDescending(b => b.CreatedAt) // Mới nhất sau
                    .Take(5)
                    .ToListAsync();
            }
            catch
            {
                return new List<Banner>();
            }
        }

        // ==========================================
        // 2. API LẤY THÔNG BÁO (Dùng cho Navbar)
        // ==========================================
        [HttpGet]
        public IActionResult GetNotifications()
        {
            var username = User.Identity.Name;
            var user = _context.AppUsers.FirstOrDefault(u => u.Username == username);

            if (user == null) return Json(new { count = 0, list = new object[] { } });

            // A. Thông báo cá nhân
            var personalNotis = _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new
                {
                    Type = "Personal",
                    Message = n.Message,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead
                })
                .ToList();

            // B. Thông báo hệ thống (Optional)
            var systemNotis = new List<dynamic>();
            try
            {
                systemNotis = _context.SystemNotifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(3)
                    .Select(n => new {
                        Type = "System",
                        Message = $"📢 {n.Title}: {n.Content}",
                        CreatedAt = n.CreatedAt,
                        IsRead = false
                    })
                    .ToList<dynamic>();
            }
            catch { }

            // C. Gộp & Sắp xếp
            var mergedList = personalNotis.Concat(systemNotis)
                .OrderByDescending(x => x.CreatedAt)
                .Take(7)
                .ToList();

            var unreadCount = _context.Notifications.Count(n => n.UserId == user.Id && !n.IsRead);

            return Json(new { count = unreadCount, list = mergedList });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}