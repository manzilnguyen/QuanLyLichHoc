using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notiHub; // Inject thêm NotificationHub

        public ChatHub(ApplicationDbContext context, IHubContext<NotificationHub> notiHub)
        {
            _context = context;
            _notiHub = notiHub;
        }

        public async Task JoinRoom(string roomName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
        }

        public async Task LeaveRoom(string roomName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        }

        // --- GỬI TIN NHẮN THÔNG MINH + BẮN THÔNG BÁO ---
        public async Task SendMessage(string roomName, string username, string content, string fileUrl, int type)
        {
            // 1. Tìm người gửi và thông tin chi tiết
            var sender = await _context.AppUsers
                .Include(u => u.Student).ThenInclude(s => s.Class)
                .Include(u => u.Lecturer)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (sender != null)
            {
                // A. Lưu tin nhắn vào Database
                var chatMsg = new ChatMessage
                {
                    Content = content,
                    FileUrl = fileUrl,
                    Type = (MessageType)type,
                    RoomName = roomName,
                    SenderId = sender.Id,
                    Timestamp = DateTime.Now
                };
                _context.ChatMessages.Add(chatMsg);
                await _context.SaveChangesAsync();

                // B. Xử lý hiển thị Chat (Gửi về Client đang mở khung chat)
                string displayName = sender.Username;
                string subInfo = "";
                string avatarChar = displayName.Substring(0, 1).ToUpper();

                if (sender.Role == "Student" && sender.Student != null)
                {
                    displayName = sender.Student.FullName;
                    subInfo = sender.Student.Class != null ? sender.Student.Class.ClassName : "Chưa phân lớp";
                }
                else if (sender.Role == "Lecturer" && sender.Lecturer != null)
                {
                    displayName = sender.Lecturer.FullName;
                    subInfo = "GV Khoa " + sender.Lecturer.Department;
                }
                else if (sender.Role == "Admin") { displayName = "Quản Trị Viên"; subInfo = "Hỗ trợ"; }
                else if (sender.Role == "Parent") { displayName = "Phụ Huynh"; subInfo = "Gia đình"; }

                string time = DateTime.Now.ToString("HH:mm");
                // Gửi tin nhắn vào phòng chat
                await Clients.Group(roomName).SendAsync("ReceiveMessage", username, displayName, subInfo, content, fileUrl, type, time, avatarChar);

                // =================================================================
                // C. GỬI THÔNG BÁO REAL-TIME ("TING TING") CHO NGƯỜI NHẬN
                // =================================================================

                // Lấy danh sách thành viên trong phòng (trừ người gửi)
                var members = await _context.ChatRoomMembers
                    .Include(m => m.User)
                    .Where(m => m.RoomName == roomName && m.UserId != sender.Id)
                    .ToListAsync();

                foreach (var member in members)
                {
                    string notiTitle = "Tin nhắn mới";
                    string notiType = "Info"; // Mặc định màu xanh dương
                    string messagePreview = type == 0 ? content : (type == 1 ? "[Hình ảnh]" : "[Tập tin]");

                    if (messagePreview.Length > 40) messagePreview = messagePreview.Substring(0, 40) + "...";

                    // [LOGIC THÔNG MINH]: Phụ huynh nhắn -> Giảng viên nhận thông báo ĐỎ (Danger)
                    if (sender.Role == "Parent" && member.User.Role == "Lecturer")
                    {
                        notiTitle = $"🔴 PHỤ HUYNH {sender.Username} NHẮN";
                        notiType = "Danger";
                    }
                    else if (roomName.StartsWith("Class_"))
                    {
                        notiTitle = $"Nhóm lớp: {displayName}";
                    }
                    else if (roomName.StartsWith("Group_"))
                    {
                        notiTitle = $"Nhóm chat: {displayName}";
                    }

                    // Gửi thông báo qua NotificationHub tới User cụ thể
                    // Param: (Title, Message, Url, Type)
                    await _notiHub.Clients.User(member.User.Username).SendAsync("ReceiveNotification",
                        notiTitle,
                        messagePreview,
                        $"/Chat/Room?roomName={roomName}",
                        notiType
                    );
                }
            }
        }
    }
}