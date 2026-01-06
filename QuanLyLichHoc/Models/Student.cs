using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Student
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mã sinh viên")]
        [Display(Name = "Mã SV")]
        public string StudentCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ tên")]
        [Display(Name = "Họ và Tên")]
        public string FullName { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [Display(Name = "Lớp sinh hoạt")]
        public int ClassId { get; set; }

        [ForeignKey("ClassId")]
        public Class? Class { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        public string? Avatar { get; set; }

        [Display(Name = "Số điện thoại cá nhân")]
        public string? PhoneNumber { get; set; }

        // --- [MỚI] THÔNG TIN PHỤ HUYNH ---
        [Display(Name = "Họ tên Phụ huynh")]
        public string? ParentName { get; set; }

        [Display(Name = "SĐT Phụ huynh")]
        public string? ParentPhone { get; set; }
        // ---------------------------------

        public virtual AppUser? AppUser { get; set; }
    }
}