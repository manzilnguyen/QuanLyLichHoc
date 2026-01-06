namespace QuanLyLichHoc.Models.ViewModels
{
    public class AcademicReportViewModel
    {
        // Danh sách chi tiết sinh viên
        public List<AcademicStatusViewModel> Students { get; set; } = new List<AcademicStatusViewModel>();

        // Dữ liệu thống kê cho biểu đồ
        public int CountExcellent { get; set; } // Xuất sắc
        public int CountGood { get; set; }      // Khá
        public int CountAverage { get; set; }   // Trung bình
        public int CountWeak { get; set; }      // Yếu
        public int CountBanned { get; set; }    // Cấm thi (Vắng > 20%)

        public double ClassAverageScore { get; set; } // Điểm trung bình cả lớp
    }

    public class AcademicStatusViewModel
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; }
        public string FullName { get; set; }
        public string ClassName { get; set; }
        public int TotalSessions { get; set; }
        public int PresentSessions { get; set; }
        public double AttendancePercentage { get; set; }
        public double AverageScore { get; set; }
        public int GradeCount { get; set; }
        public string StatusLabel { get; set; }
        public string StatusColor { get; set; }
    }
}