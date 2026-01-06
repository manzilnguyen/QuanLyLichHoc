using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public enum EnrollmentStatus
    {
        [Display(Name = "Chờ duyệt")] Pending = 0,
        [Display(Name = "Đã duyệt")] Approved = 1,
        [Display(Name = "Từ chối")] Rejected = 2
    }

    public class Enrollment
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public Student Student { get; set; }

        public int ClassId { get; set; }
        [ForeignKey("ClassId")] public Class Class { get; set; }

        public DateTime RegisterDate { get; set; } = DateTime.Now;

        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;
    }
}