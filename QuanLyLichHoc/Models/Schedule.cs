// File: Models/Schedule.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Schedule
    {
        public int Id { get; set; }

        // --- 1. LỚP HỌC ---
        [Display(Name = "Lớp học")]
        public int ClassId { get; set; }
        [ForeignKey("ClassId")]
        public Class? Class { get; set; }

        // --- 2. MÔN HỌC ---
        [Display(Name = "Môn học")]
        public int SubjectId { get; set; }
        [ForeignKey("SubjectId")]
        public Subject? Subject { get; set; }

        // --- 3. PHÒNG HỌC ---
        [Display(Name = "Phòng học")]
        public int RoomId { get; set; }
        [ForeignKey("RoomId")]
        public Room? Room { get; set; }

        // --- 4. GIẢNG VIÊN ---
        [Display(Name = "Giảng viên")]
        public int LecturerId { get; set; }
        [ForeignKey("LecturerId")]
        public Lecturer? Lecturer { get; set; }

        // --- 5. THỜI GIAN ---
        [Required]
        [Display(Name = "Ngày học")]
        public DayOfWeek DayOfWeek { get; set; } // Thứ 2, 3, 4...

        [Required]
        [Display(Name = "Giờ bắt đầu")]
        public TimeSpan StartTime { get; set; } // VD: 07:00:00

        [Required]
        [Display(Name = "Giờ kết thúc")]
        public TimeSpan EndTime { get; set; }   // VD: 09:00:00

        [Display(Name = "Học kỳ")]
        public string Semester { get; set; } = "HK1-2025"; // Tạm fix cứng, có thể nâng cấp sau
    }
}