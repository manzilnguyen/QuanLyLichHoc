using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public enum MessageType { Text, Image, File, Video, Audio }

    public class ChatMessage
    {
        public int Id { get; set; }
        public string? Content { get; set; } // Nội dung text hoặc tên file
        public string? FileUrl { get; set; } // Đường dẫn file/ảnh
        public MessageType Type { get; set; } = MessageType.Text;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public int SenderId { get; set; }
        [ForeignKey("SenderId")] public AppUser Sender { get; set; }

        public string RoomName { get; set; }
    }
}
