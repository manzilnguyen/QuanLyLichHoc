using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public enum AttendanceStatus
    {
        [Display(Name = "Chờ duyệt")] Pending = 0,
        [Display(Name = "Có mặt")] Approved = 1,
        [Display(Name = "Vắng")] Rejected = 2
    }

    public class Attendance
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        [ForeignKey("ScheduleId")] public Schedule? Schedule { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public Student? Student { get; set; }

        public DateTime Date { get; set; }

        public string? ProofImage { get; set; } // Ảnh minh chứng (cho trường hợp điểm danh online)

        public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;

        // MỚI: Ghi chú của giảng viên (VD: Đi muộn, Có phép...)
        public string? Note { get; set; }
    }
}