using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class Banner
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        [Display(Name = "Tiêu đề")]
        public string Title { get; set; }

        [Display(Name = "Hình ảnh")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Liên kết (URL)")]
        public string? LinkUrl { get; set; }

        [Display(Name = "Mô tả ngắn")]
        public string? Description { get; set; }

        [Display(Name = "Thứ tự")]
        public int Priority { get; set; } = 1; // Sắp xếp: Số lớn hiện trước hoặc ngược lại

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}