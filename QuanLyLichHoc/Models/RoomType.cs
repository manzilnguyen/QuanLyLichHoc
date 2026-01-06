// File: Models/RoomType.cs
using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public enum RoomType
    {
        [Display(Name = "Phòng Lý thuyết")]
        Theory = 1,

        [Display(Name = "Phòng Thực hành/Lab")]
        Practice = 2,

        [Display(Name = "Hội trường")]
        Hall = 3
    }
}