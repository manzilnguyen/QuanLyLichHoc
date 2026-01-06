using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Lecturer
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        [StringLength(100)]
        [Display(Name = "Họ và Tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [StringLength(15)]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Khoa / Bộ môn")]
        public string? Department { get; set; }

        public string? Address { get; set; }
        public string? Avatar { get; set; }

        // --- SỬA LỖI: Bỏ int AppUserId, chỉ giữ navigation property ---
        public virtual AppUser? AppUser { get; set; }

        public virtual ICollection<Schedule>? Schedules { get; set; }
        public virtual ICollection<Class>? HomeClasses { get; set; }
    }
}