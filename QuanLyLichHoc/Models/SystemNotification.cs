using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class SystemNotification
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        public string Title { get; set; } // VD: Thay đổi lịch học

        [Required(ErrorMessage = "Vui lòng nhập nội dung")]
        public string Content { get; set; } // VD: Lớp SE1 nghỉ chiều nay...

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Loại thông báo để hiện màu cho đẹp (Info, Warning, Danger)
        public string Type { get; set; } = "Info";
    }
}