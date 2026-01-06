using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class TuitionFee
    {
        [Key]
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public virtual Student? Student { get; set; }

        [Required]
        [Display(Name = "Số tiền")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Học kỳ")]
        public string Semester { get; set; } // VD: HK1-2025

        [Display(Name = "Nội dung thu")]
        public string Title { get; set; } // VD: Học phí kỳ 1

        public bool IsPaid { get; set; } = false; // Trạng thái thanh toán

        public DateTime? PaymentDate { get; set; } // Ngày đóng

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}