using Microsoft.AspNetCore.SignalR;

namespace QuanLyLichHoc.Hubs
{
    public class NotificationHub : Hub
    {
        // Hàm này để Client gọi lên nếu muốn đánh dấu đã đọc (Optional)
        public async Task MarkAsRead(int notificationId)
        {
            // Logic xử lý DB ở đây nếu cần, hoặc gọi API
        }
    }
}