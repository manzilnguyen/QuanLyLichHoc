using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public string Title { get; set; }   // Tiêu đề (VD: Điểm mới)
        public string Message { get; set; } // Nội dung
        public string? Url { get; set; }    // Link liên kết
        public string Type { get; set; } = "Info"; // Info, Success, Warning, Danger

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual AppUser? AppUser { get; set; }
    }
}