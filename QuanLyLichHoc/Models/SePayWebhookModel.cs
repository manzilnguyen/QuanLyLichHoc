namespace QuanLyLichHoc.Models
{
    public class SePayWebhookModel
    {
        public long Id { get; set; }
        public string Gateway { get; set; }
        public string TransactionDate { get; set; }
        public string AccountNumber { get; set; }
        public string SubAccount { get; set; }
        public string Content { get; set; } // Nội dung CK (Quan trọng: chứa mã HPxxx)
        public decimal TransferAmount { get; set; } // Số tiền
        public string TransferType { get; set; }
        public long Accumulated { get; set; }
        public string Code { get; set; }
        public string ReferenceCode { get; set; }
        public string Description { get; set; }
    }
}