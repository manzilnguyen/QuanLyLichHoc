using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class ChatRoomInfo
    {
        [Key]
        public string RoomName { get; set; } // ID Kỹ thuật (VD: Guid_123)

        [Required]
        public string DisplayName { get; set; } // Tên hiển thị (VD: Nhóm Dự Án A)

        public string? AvatarUrl { get; set; } // Ảnh đại diện nhóm

        public int CreatorId { get; set; } // Người tạo (để check quyền sửa)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}