// File: Models/Room.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên phòng")]
        [Display(Name = "Tên phòng")]
        public string RoomName { get; set; } // Ví dụ: A101, B202

        [Required]
        [Range(10, 500, ErrorMessage = "Sức chứa từ 10 đến 500")]
        [Display(Name = "Sức chứa")]
        public int Capacity { get; set; }

        [Required]
        [Display(Name = "Loại phòng")]
        public RoomType Type { get; set; }
    }
}