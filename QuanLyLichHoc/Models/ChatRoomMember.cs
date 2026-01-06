using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class ChatRoomMember
    {
        public int Id { get; set; }
        public string RoomName { get; set; }
        public int UserId { get; set; }
        [ForeignKey("UserId")] public AppUser User { get; set; }
    }
}
