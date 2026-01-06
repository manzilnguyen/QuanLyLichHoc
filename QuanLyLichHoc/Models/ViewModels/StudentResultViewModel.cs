namespace QuanLyLichHoc.Models.ViewModels
{
    public class StudentResultViewModel
    {
        public Student Student { get; set; }
        public List<Grade> Grades { get; set; } // Danh sách điểm chi tiết

        // Điểm trung bình
        public double GpaHK1 { get; set; }
        public double GpaHK2 { get; set; }
        public double GpaYear { get; set; } // Cả năm

        // Xếp loại
        public string RankHK1 { get; set; }
        public string RankHK2 { get; set; }
        public string RankYear { get; set; }
    }
}