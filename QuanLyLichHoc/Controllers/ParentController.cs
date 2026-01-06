using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Parent")]
    public class ParentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ParentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- HÀM HỖ TRỢ: TÌM CON CÁI ---
        private async Task<Student?> GetMyChild()
        {
            // Quy tắc: TK Phụ huynh = "ABC_PH" -> TK Con = "ABC"
            var parentUsername = User.Identity.Name;
            if (string.IsNullOrEmpty(parentUsername)) return null;

            // Cắt đuôi "PH" (Lấy phần username của con)
            string studentUsername = parentUsername.Substring(0, parentUsername.Length - 2);

            var studentUser = await _context.AppUsers
                .Include(u => u.Student)
                .ThenInclude(s => s.Class)
                .FirstOrDefaultAsync(u => u.Username == studentUsername);

            return studentUser?.Student;
        }

        // 1. DASHBOARD THÔNG MINH (Smart Dashboard)
        public async Task<IActionResult> Dashboard()
        {
            var student = await GetMyChild();
            if (student == null) return View("Error", "Không tìm thấy dữ liệu học sinh liên kết.");

            // A. TÍNH ĐIỂM TRUNG BÌNH (GPA)
            var grades = await _context.Grades.Where(g => g.StudentId == student.Id).ToListAsync();
            double gpa = 0;
            if (grades.Any())
            {
                gpa = grades.Average(g => g.Score);
            }

            // B. TÍNH TỶ LỆ CHUYÊN CẦN
            var attendance = await _context.Attendances.Where(a => a.StudentId == student.Id).ToListAsync();
            int totalSlots = attendance.Count;
            int presentSlots = attendance.Count(a => a.Status == AttendanceStatus.Approved);
            double attendanceRate = totalSlots > 0 ? (double)presentSlots / totalSlots * 100 : 100;

            // C. TRUYỀN DỮ LIỆU SANG VIEW
            ViewBag.GPA = Math.Round(gpa, 2);
            ViewBag.AttendanceRate = Math.Round(attendanceRate, 1);

            // Đánh giá xếp loại
            if (gpa >= 8.0) ViewBag.AcademicRank = "Giỏi";
            else if (gpa >= 6.5) ViewBag.AcademicRank = "Khá";
            else if (gpa >= 5.0) ViewBag.AcademicRank = "Trung bình";
            else ViewBag.AcademicRank = "Yếu";

            return View(student);
        }

        // 2. XEM BẢNG ĐIỂM
        public async Task<IActionResult> ChildScores()
        {
            var student = await GetMyChild();
            if (student == null) return NotFound();

            var grades = await _context.Grades
                .Include(g => g.Subject)
                .Where(g => g.StudentId == student.Id)
                .OrderBy(g => g.Semester)
                .ToListAsync();

            ViewBag.StudentName = student.FullName;
            return View(grades);
        }

        // 3. XEM LỊCH HỌC
        public async Task<IActionResult> ChildSchedule()
        {
            var student = await GetMyChild();
            if (student == null) return NotFound();

            var schedules = await _context.Schedules
                .Include(s => s.Subject).Include(s => s.Room).Include(s => s.Lecturer)
                .Where(s => s.ClassId == student.ClassId)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                .ToListAsync();

            ViewBag.StudentName = student.FullName;
            return View(schedules);
        }

        // 4. XEM ĐIỂM DANH
        public async Task<IActionResult> ChildAttendance()
        {
            var student = await GetMyChild();
            if (student == null) return NotFound();

            var attendances = await _context.Attendances
                .Include(a => a.Schedule).ThenInclude(s => s.Subject)
                .Where(a => a.StudentId == student.Id)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            ViewBag.Vang = attendances.Count(a => a.Status == AttendanceStatus.Rejected);
            ViewBag.DiHoc = attendances.Count(a => a.Status == AttendanceStatus.Approved);

            return View(attendances);
        }

        // 5. LIÊN HỆ GIẢNG VIÊN
        public async Task<IActionResult> ContactLecturers()
        {
            var student = await GetMyChild();
            if (student == null) return NotFound();

            var lecturers = await _context.Schedules
                .Include(s => s.Lecturer)
                .ThenInclude(l => l.AppUser) // Include để lấy ID chat
                .Where(s => s.ClassId == student.ClassId)
                .Select(s => s.Lecturer)
                .Distinct()
                .ToListAsync();

            // Lọc unique theo ID để tránh trùng lặp
            var uniqueLecturers = lecturers.DistinctBy(l => l.Id).ToList();

            return View(uniqueLecturers);
        }
    }
}