using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyLichHoc.Models
{
    public class Document
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } // Tên hiển thị

        public string FilePath { get; set; } // Đường dẫn file

        public DateTime UploadDate { get; set; } = DateTime.Now;

        // Tài liệu thuộc về Lớp nào
        public int ClassId { get; set; }
        [ForeignKey("ClassId")] public Class Class { get; set; }

        // Ai upload (Giảng viên)
        public int UploaderId { get; set; }
    }
}