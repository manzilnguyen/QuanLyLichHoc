using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public enum LeaveStatus
    {
        [Display(Name = "Chờ duyệt")] Pending = 0,
        [Display(Name = "Đã duyệt")] Approved = 1,
        [Display(Name = "Bị từ chối")] Rejected = 2
    }

    public class LeaveRequest
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public Student Student { get; set; }

        public int ClassId { get; set; }
        [ForeignKey("ClassId")] public Class Class { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày nghỉ")]
        public DateTime LeaveDate { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lý do")]
        [StringLength(500)]
        public string Reason { get; set; }

        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        // MỚI: Ghi chú phản hồi từ Giảng viên (VD: "Lý do không chính đáng")
        public string? ResponseNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}