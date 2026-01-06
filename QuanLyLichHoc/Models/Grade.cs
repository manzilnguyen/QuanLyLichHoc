// File: Models/Grade.cs
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace QuanLyLichHoc.Models
{
    public class Grade
    {
        public int Id { get; set; }

        public int StudentId { get; set; }
        [ForeignKey("StudentId")] public Student Student { get; set; }

        public int SubjectId { get; set; }
        [ForeignKey("SubjectId")] public Subject Subject { get; set; }

        [Display(Name = "Điểm số")]
        [Range(0, 10)]
        public double Score { get; set; } // Điểm hệ 10

        [Display(Name = "Học kỳ")]
        public string Semester { get; set; } = "HK1-2025";
    }
}