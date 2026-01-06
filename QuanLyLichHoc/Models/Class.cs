// File: Models/Class.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Class
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Tên lớp")]
        [Display(Name = "Tên lớp")]
        public string ClassName { get; set; } // Ví dụ: CNTT-K15A

        [Required]
        [Display(Name = "Khóa/Năm học")]
        public string AcademicYear { get; set; } // Ví dụ: 2024-2028

        // --- KHÓA NGOẠI (Liên kết với Giảng viên) ---
        [Display(Name = "Giảng viên chủ nhiệm")]
        public int? LecturerId { get; set; } // Có thể null nếu chưa có GVCN

        [ForeignKey("LecturerId")]
        public Lecturer? Lecturer { get; set; } // Biến điều hướng để lấy thông tin GV
        [Display(Name = "Sĩ số tối đa")]
        public int MaxQuantity { get; set; } = 30;
    }
}