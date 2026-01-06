using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class LecturerEvaluation
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

        public int LecturerId { get; set; }
        [ForeignKey("LecturerId")]
        public virtual Lecturer? Lecturer { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5")]
        public int Rating { get; set; } // 1 đến 5 sao

        [Required(ErrorMessage = "Vui lòng nhập nhận xét")]
        [StringLength(500)]
        public string Comment { get; set; }

        public bool IsAnonymous { get; set; } = false; // Đánh giá ẩn danh

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}