namespace QuanLyLichHoc.Models
{
    public class SePayWebhookData
    {
        public string Gateway { get; set; }
        public string TransactionDate { get; set; }
        public string AccountNumber { get; set; }
        public string Content { get; set; } // Nội dung CK (VD: HP105)
        public decimal TransferAmount { get; set; } // Số tiền chuyển
        public decimal Accumularted { get; set; }
        public string ReferenceCode { get; set; } // Mã tham chiếu ngân hàng
        public string Description { get; set; }
    }
}