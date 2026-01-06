// File: Models/Subject.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class Subject
    {
        public int Id { get; set; } // Khóa chính

        [Required(ErrorMessage = "Vui lòng nhập Mã môn học")]
        [StringLength(50)]
        [Display(Name = "Mã môn học")]
        public string SubjectCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Tên môn học")]
        [StringLength(200)]
        [Display(Name = "Tên môn học")]
        public string SubjectName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tín chỉ")]
        [Range(1, 10, ErrorMessage = "Số tín chỉ phải từ 1 đến 10")]
        [Display(Name = "Số tín chỉ")]
        public int Credits { get; set; }
    }
}