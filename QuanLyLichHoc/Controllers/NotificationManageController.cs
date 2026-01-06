using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Hubs;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")] // Chỉ Admin mới được vào
    public class NotificationManageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext; // Dùng chung Hub Chat để gửi thông báo

        public NotificationManageController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 1. Danh sách thông báo đã gửi
        public async Task<IActionResult> Index()
        {
            // Kiểm tra xem bảng này có tồn tại chưa, nếu chưa có thì trả về view rỗng để tránh lỗi
            try
            {
                var list = await _context.SystemNotifications.OrderByDescending(n => n.CreatedAt).ToListAsync();
                return View(list);
            }
            catch
            {
                return View(new List<SystemNotification>());
            }
        }

        // 2. Trang tạo thông báo
        public IActionResult Create()
        {
            return View();
        }

        // 3. Xử lý Gửi (Lưu DB + Bắn SignalR)
        [HttpPost]
        public async Task<IActionResult> Create(SystemNotification model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Add(model);
                await _context.SaveChangesAsync();

                // Gửi SignalR tới tất cả Client đang online
                // Hàm "ReceiveSystemNotification" phải khớp với bên _Layout.cshtml
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", model.Title, model.Content, model.Type, model.CreatedAt.ToString("HH:mm dd/MM"));

                TempData["Success"] = "Đã gửi thông báo thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // 4. Xóa thông báo
        public async Task<IActionResult> Delete(int id)
        {
            var noti = await _context.SystemNotifications.FindAsync(id);
            if (noti != null)
            {
                _context.SystemNotifications.Remove(noti);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}