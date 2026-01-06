using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using QuanLyLichHoc.Models.ViewModels;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin,Lecturer")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> AcademicStatus(int? classId)
        {
            // 1. Dropdown Lớp
            IQueryable<Class> classQuery = _context.Classes;
            if (User.IsInRole("Lecturer"))
            {
                var lecIdStr = User.FindFirst("LecturerId")?.Value;
                if (lecIdStr != null)
                {
                    int lecId = int.Parse(lecIdStr);
                    classQuery = classQuery.Where(c => c.LecturerId == lecId);
                }
            }
            ViewData["ClassId"] = new SelectList(await classQuery.ToListAsync(), "Id", "ClassName", classId);

            // 2. ViewModel chính
            var viewModel = new AcademicReportViewModel();

            if (classId == null) return View(viewModel);

            // 3. Xử lý dữ liệu
            var students = await _context.Students.Where(s => s.ClassId == classId).Include(s => s.Class).ToListAsync();

            // Tối ưu query: Lấy hết dữ liệu cần thiết 1 lần thay vì query trong vòng lặp
            var allAttendances = await _context.Attendances
                .Where(a => a.Schedule.ClassId == classId)
                .ToListAsync();

            var allGrades = await _context.Grades
                .Where(g => g.Student.ClassId == classId)
                .ToListAsync();

            foreach (var sv in students)
            {
                // Filter dữ liệu trong bộ nhớ (nhanh hơn query DB nhiều lần)
                var atts = allAttendances.Where(a => a.StudentId == sv.Id).ToList();
                var grades = allGrades.Where(g => g.StudentId == sv.Id).ToList();

                int totalAtt = atts.Count;
                int present = atts.Count(a => a.Status == AttendanceStatus.Approved);
                double attPercent = totalAtt > 0 ? (double)present / totalAtt * 100 : 100;
                double avgScore = grades.Any() ? Math.Round(grades.Average(g => g.Score), 1) : 0;

                string label = "Bình thường";
                string color = "text-dark";

                // Phân loại
                if (attPercent < 80)
                {
                    label = "Cấm thi"; color = "text-danger";
                    viewModel.CountBanned++;
                }
                else if (grades.Any())
                {
                    if (avgScore >= 8.5) { label = "Xuất sắc"; color = "text-success"; viewModel.CountExcellent++; }
                    else if (avgScore >= 7.0) { label = "Khá"; color = "text-primary"; viewModel.CountGood++; }
                    else if (avgScore >= 5.0) { label = "Trung bình"; color = "text-warning"; viewModel.CountAverage++; }
                    else { label = "Yếu"; color = "text-muted"; viewModel.CountWeak++; }
                }
                else
                {
                    label = "Chưa có điểm";
                }

                viewModel.Students.Add(new AcademicStatusViewModel
                {
                    StudentId = sv.Id,
                    StudentCode = sv.StudentCode,
                    FullName = sv.FullName,
                    AttendancePercentage = Math.Round(attPercent, 0),
                    AverageScore = avgScore,
                    StatusLabel = label,
                    StatusColor = color
                });
            }

            if (viewModel.Students.Any(s => s.AverageScore > 0))
            {
                viewModel.ClassAverageScore = Math.Round(viewModel.Students.Where(s => s.AverageScore > 0).Average(s => s.AverageScore), 1);
            }

            return View(viewModel);
        }
    }
}