using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Tên đăng nhập")]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; } // "Admin", "Lecturer", "Student", "Parent"

        // =========================================================
        // BỔ SUNG 2 THUỘC TÍNH NÀY ĐỂ SỬA LỖI BIÊN DỊCH
        // =========================================================
        public bool IsActive { get; set; } = true; // Trạng thái kích hoạt (Mặc định: True)
        public DateTime CreatedAt { get; set; } = DateTime.Now; // Ngày tạo (Mặc định: Bây giờ)

        // =========================================================
        // LIÊN KẾT DỮ LIỆU (FOREIGN KEYS)
        // =========================================================

        public int? LecturerId { get; set; }
        [ForeignKey("LecturerId")]
        public virtual Lecturer? Lecturer { get; set; }

        public int? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }
    }
}
