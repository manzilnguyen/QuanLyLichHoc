using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ChatController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var rooms = await GetChatRoomsForUser();
            return View(rooms);
        }

        public async Task<IActionResult> Contacts()
        {
            var username = User.Identity.Name;
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);

            if (User.IsInRole("Parent"))
            {
                string studentCode = username.Replace("PH", "");
                var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode);

                if (student == null) return View("Error", "Không tìm thấy học sinh liên kết.");

                var lecturers = await _context.Schedules
                    .Where(s => s.ClassId == student.ClassId)
                    .Select(s => s.Lecturer)
                    .Distinct()
                    .Include(l => l.AppUser)
                    .ToListAsync();

                ViewBag.Contacts = lecturers;
                ViewBag.Type = "ParentView";
            }
            else if (User.IsInRole("Lecturer"))
            {
                var classIds = await _context.Schedules
                    .Where(s => s.LecturerId == currentUser.LecturerId)
                    .Select(s => s.ClassId)
                    .Distinct()
                    .ToListAsync();

                var students = await _context.Students
                    .Include(s => s.Class)
                    .Where(s => classIds.Contains(s.ClassId))
                    .ToListAsync();

                var parents = new List<dynamic>();
                foreach (var st in students)
                {
                    var phUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == st.StudentCode + "PH");
                    if (phUser != null)
                    {
                        parents.Add(new
                        {
                            Id = phUser.Id,
                            FullName = $"PH em {st.FullName}",
                            SubInfo = $"Lớp {st.Class.ClassName}",
                            Avatar = ""
                        });
                    }
                }
                ViewBag.Contacts = parents;
                ViewBag.Type = "LecturerView";
            }

            return View();
        }

        public async Task<IActionResult> StartPrivateChat(int targetUserId)
        {
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            int minId = Math.Min(currentUser.Id, targetUserId);
            int maxId = Math.Max(currentUser.Id, targetUserId);
            string roomName = $"Private_{minId}_{maxId}";

            if (!_context.ChatRoomMembers.Any(m => m.RoomName == roomName && m.UserId == currentUser.Id))
                _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = currentUser.Id });

            if (!_context.ChatRoomMembers.Any(m => m.RoomName == roomName && m.UserId == targetUserId))
                _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = targetUserId });

            await _context.SaveChangesAsync();
            return RedirectToAction("Room", new { roomName = roomName });
        }

        // ============================================================
        // 4. ROOM (VÀO PHÒNG CHAT) - CẬP NHẬT PHÂN QUYỀN
        // ============================================================
        public async Task<IActionResult> Room(string roomName)
        {
            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return RedirectToAction("Login", "Account");

            // A. Auto-Join cho Admin/GVCN vào phòng Lớp/PH nếu chưa join
            if (!roomName.StartsWith("Group_") && !_context.ChatRoomMembers.Any(m => m.RoomName == roomName && m.UserId == user.Id))
            {
                if (User.IsInRole("Admin") || User.IsInRole("Lecturer"))
                {
                    // Admin hoặc GV được quyền tự vào xem các nhóm lớp/phụ huynh
                    _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = user.Id });
                    await _context.SaveChangesAsync();
                }
                else if (roomName.StartsWith("Private_"))
                {
                    _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = user.Id });
                    await _context.SaveChangesAsync();
                }
            }

            // B. Bảo mật Group kín (Group tự tạo)
            if (roomName.StartsWith("Group_") && !_context.ChatRoomMembers.Any(m => m.RoomName == roomName && m.UserId == user.Id))
                return RedirectToAction("Index");

            var roomInfo = await _context.ChatRoomInfos.FindAsync(roomName);

            // C. --- TÍNH TOÁN QUYỀN QUẢN LÝ (CanManage) ---
            bool canManage = false;

            // 1. Admin luôn có quyền
            if (User.IsInRole("Admin"))
            {
                canManage = true;
            }
            // 2. Chủ phòng (người tạo) có quyền
            else if (roomInfo != null && roomInfo.CreatorId == user.Id)
            {
                canManage = true;
            }
            // 3. Giảng viên chủ nhiệm/dạy lớp có quyền quản lý nhóm Lớp & Nhóm PH
            else if (User.IsInRole("Lecturer") && (roomName.StartsWith("Class_") || roomName.StartsWith("ParentGroup_")))
            {
                var parts = roomName.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[1], out int classId))
                {
                    // Kiểm tra xem GV này có phụ trách lớp này không
                    var isMyClass = await _context.Classes.AnyAsync(c => c.Id == classId && c.LecturerId == user.LecturerId);
                    if (isMyClass) canManage = true;
                }
            }

            ViewBag.CanManage = canManage;
            // ---------------------------------------------

            // D. Xử lý hiển thị Tên & Avatar
            if (roomInfo != null)
            {
                ViewBag.RoomDisplayName = roomInfo.DisplayName;
                ViewBag.RoomAvatar = roomInfo.AvatarUrl;
            }
            else
            {
                if (roomName.StartsWith("Private_"))
                {
                    var parts = roomName.Split('_');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int id1) && int.TryParse(parts[2], out int id2))
                    {
                        int partnerId = (id1 == user.Id) ? id2 : id1;
                        var partner = await _context.AppUsers.Include(u => u.Lecturer).FirstOrDefaultAsync(u => u.Id == partnerId);

                        string dName = partner?.Username ?? "Unknown";
                        if (partner?.Role == "Lecturer") dName = "GV. " + partner.Lecturer?.FullName;
                        if (partner?.Role == "Parent") dName = "PH " + partner.Username.Replace("PH", "");
                        ViewBag.RoomDisplayName = dName;
                    }
                    else
                    {
                        ViewBag.RoomDisplayName = "Hỗ trợ";
                    }
                }
                else if (roomName.StartsWith("ParentGroup_"))
                {
                    // Lấy tên lớp để hiển thị đẹp
                    int cId = int.Parse(roomName.Split('_')[1]);
                    var cls = await _context.Classes.FindAsync(cId);
                    ViewBag.RoomDisplayName = cls != null ? $"Hội PH Lớp {cls.ClassName}" : "Hội Phụ Huynh";
                }
                else if (roomName.StartsWith("Class_"))
                {
                    int cId = int.Parse(roomName.Split('_')[1]);
                    var cls = await _context.Classes.FindAsync(cId);
                    ViewBag.RoomDisplayName = cls != null ? $"Lớp {cls.ClassName}" : "Lớp học";
                }
                else
                {
                    ViewBag.RoomDisplayName = roomName;
                }
                ViewBag.RoomAvatar = null;
            }

            var history = await _context.ChatMessages.Include(m => m.Sender).Where(m => m.RoomName == roomName).OrderByDescending(m => m.Timestamp).Take(50).OrderBy(m => m.Timestamp).ToListAsync();
            var members = await _context.ChatRoomMembers.Include(m => m.User).Where(m => m.RoomName == roomName).Select(m => m.User).ToListAsync();

            ViewBag.SidebarRooms = await GetChatRoomsForUser();
            ViewBag.RoomName = roomName;
            ViewBag.CurrentUser = username;
            ViewBag.Members = members;

            return View(history);
        }

        // ============================================================
        // 5. CÁC HÀM QUẢN LÝ (KICK, DELETE) - CẬP NHẬT CHECK QUYỀN
        // ============================================================

        [HttpPost]
        public async Task<IActionResult> KickMember(string roomName, int userId)
        {
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            // Check quyền (Logic giống hệt hàm Room)
            bool hasRight = false;
            if (User.IsInRole("Admin")) hasRight = true;
            else
            {
                var info = await _context.ChatRoomInfos.FindAsync(roomName);
                if (info != null && info.CreatorId == currentUser.Id) hasRight = true;
                else if (User.IsInRole("Lecturer") && (roomName.StartsWith("Class_") || roomName.StartsWith("ParentGroup_")))
                {
                    var parts = roomName.Split('_');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int classId))
                    {
                        if (await _context.Classes.AnyAsync(c => c.Id == classId && c.LecturerId == currentUser.LecturerId)) hasRight = true;
                    }
                }
            }

            if (!hasRight) return Forbid();

            var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.RoomName == roomName && m.UserId == userId);
            if (member != null) { _context.ChatRoomMembers.Remove(member); await _context.SaveChangesAsync(); return Ok(); }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteChatRoom(string roomName)
        {
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            // Chỉ Admin hoặc Chủ phòng tự tạo mới được xóa phòng
            // (GVCN không xóa phòng hệ thống ParentGroup/Class, nhưng có thể kick thành viên)
            bool hasRight = false;
            if (User.IsInRole("Admin")) hasRight = true;
            else
            {
                var info = await _context.ChatRoomInfos.FindAsync(roomName);
                if (info != null && info.CreatorId == currentUser.Id) hasRight = true;
            }

            if (!hasRight) return Forbid();

            var infoDel = await _context.ChatRoomInfos.FindAsync(roomName);
            if (infoDel != null) _context.ChatRoomInfos.Remove(infoDel);

            var members = _context.ChatRoomMembers.Where(m => m.RoomName == roomName); _context.ChatRoomMembers.RemoveRange(members);
            var messages = _context.ChatMessages.Where(m => m.RoomName == roomName); _context.ChatMessages.RemoveRange(messages);

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Lỗi file.");
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "chat_uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            string uniqueName = Guid.NewGuid() + "_" + file.FileName;
            using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueName), FileMode.Create)) { await file.CopyToAsync(stream); }
            return Json(new { url = "/chat_uploads/" + uniqueName, fileName = file.FileName });
        }

        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> CreateGroup()
        {
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            List<AppUser> usersAvailable = new List<AppUser>();

            if (User.IsInRole("Admin"))
                usersAvailable = await _context.AppUsers.Where(u => u.Id != currentUser.Id).ToListAsync();
            else if (User.IsInRole("Lecturer"))
            {
                var classIds = await _context.Classes.Where(c => c.LecturerId == currentUser.LecturerId).Select(c => c.Id).ToListAsync();
                usersAvailable = await _context.AppUsers.Include(u => u.Student)
                    .Where(u => u.Role == "Student" && u.StudentId != null && classIds.Contains(u.Student.ClassId))
                    .ToListAsync();
            }

            ViewBag.Users = new MultiSelectList(usersAvailable, "Id", "Username");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(string groupName, IFormFile? avatar, List<int> memberIds)
        {
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            string roomName = "Group_" + Guid.NewGuid().ToString();
            string avatarPath = null;
            if (avatar != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "chat_uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                string uniqueName = Guid.NewGuid() + "_" + avatar.FileName;
                using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueName), FileMode.Create)) { await avatar.CopyToAsync(stream); }
                avatarPath = "/chat_uploads/" + uniqueName;
            }

            var info = new ChatRoomInfo { RoomName = roomName, DisplayName = groupName, AvatarUrl = avatarPath, CreatorId = currentUser.Id, CreatedAt = DateTime.Now };
            _context.ChatRoomInfos.Add(info);
            _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = currentUser.Id });

            if (memberIds != null)
            {
                foreach (var uid in memberIds) if (uid != currentUser.Id) _context.ChatRoomMembers.Add(new ChatRoomMember { RoomName = roomName, UserId = uid });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Room", new { roomName = roomName });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRoomInfo(string roomName, string newName, IFormFile? newAvatar)
        {
            var info = await _context.ChatRoomInfos.FindAsync(roomName);
            var currentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            if (info == null)
            {
                // Nếu phòng chưa có info (phòng hệ thống), tạo info mới
                info = new ChatRoomInfo { RoomName = roomName, DisplayName = newName, CreatorId = currentUser.Id };
                _context.ChatRoomInfos.Add(info);
            }
            else
            {
                if (!User.IsInRole("Admin") && info.CreatorId != currentUser.Id) return Forbid();
                if (!string.IsNullOrEmpty(newName)) info.DisplayName = newName;
            }
            if (newAvatar != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "chat_uploads");
                string uniqueName = Guid.NewGuid() + "_" + newAvatar.FileName;
                using (var stream = new FileStream(Path.Combine(uploadsFolder, uniqueName), FileMode.Create)) { await newAvatar.CopyToAsync(stream); }
                info.AvatarUrl = "/chat_uploads/" + uniqueName;
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Room", new { roomName = roomName });
        }

        // HELPER: LẤY DANH SÁCH PHÒNG
        private async Task<List<ChatRoomViewModel>> GetChatRoomsForUser()
        {
            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);
            var chatRooms = new List<ChatRoomViewModel>();

            var joinedRooms = await _context.ChatRoomMembers.Where(m => m.UserId == user.Id).Select(m => m.RoomName).ToListAsync();

            // 1. Add các phòng đã tham gia
            foreach (var rName in joinedRooms)
            {
                if (rName.StartsWith("Group_"))
                {
                    var info = await _context.ChatRoomInfos.FindAsync(rName);
                    if (info != null) chatRooms.Add(new ChatRoomViewModel { RoomName = rName, DisplayName = info.DisplayName, Avatar = info.AvatarUrl, Type = "Group" });
                }
                else if (rName.StartsWith("Private_") && !rName.Contains("Admin"))
                {
                    var parts = rName.Split('_');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int id1) && int.TryParse(parts[2], out int id2))
                    {
                        int partnerId = (id1 == user.Id) ? id2 : id1;
                        var partner = await _context.AppUsers.Include(u => u.Lecturer).FirstOrDefaultAsync(u => u.Id == partnerId);

                        string dName = partner?.Username ?? "Unknown";
                        if (partner?.Role == "Lecturer") dName = "GV. " + partner.Lecturer?.FullName;
                        if (partner?.Role == "Parent") dName = "PH " + partner.Username.Replace("PH", "");
                        chatRooms.Add(new ChatRoomViewModel { RoomName = rName, DisplayName = dName, Type = "Private" });
                    }
                }
            }

            // 2. Add phòng Hệ thống (Admin & GV thấy lớp/nhóm PH mình quản lý)
            if (User.IsInRole("Admin"))
            {
                // ADMIN THẤY TẤT CẢ LỚP HỌC VÀ NHÓM PHỤ HUYNH
                var allClasses = await _context.Classes.ToListAsync();
                foreach (var c in allClasses)
                {
                    if (!chatRooms.Any(r => r.RoomName == $"Class_{c.Id}"))
                        chatRooms.Add(new ChatRoomViewModel { RoomName = $"Class_{c.Id}", DisplayName = $"🏫 {c.ClassName}", Type = "Class" });

                    if (!chatRooms.Any(r => r.RoomName == $"ParentGroup_{c.Id}"))
                        chatRooms.Add(new ChatRoomViewModel { RoomName = $"ParentGroup_{c.Id}", DisplayName = $"👨‍👩‍👧‍👦 Hội PH {c.ClassName}", Type = "Group" });
                }

                // Thêm phòng Support
                var supportRooms = await _context.ChatMessages.Where(m => m.RoomName.StartsWith("Private_Admin_")).Select(m => m.RoomName).Distinct().ToListAsync();
                foreach (var room in supportRooms)
                    if (!chatRooms.Any(r => r.RoomName == room))
                        chatRooms.Add(new ChatRoomViewModel { RoomName = room, DisplayName = "📩 Hỗ trợ", Type = "Support" });
            }
            else
            {
                if (!chatRooms.Any(r => r.RoomName == $"Private_Admin_{user.Id}"))
                    chatRooms.Add(new ChatRoomViewModel { RoomName = $"Private_Admin_{user.Id}", DisplayName = "💬 Hỗ trợ Admin", Type = "Support" });

                if (User.IsInRole("Lecturer") && user.LecturerId != null)
                {
                    // LECTURER THẤY LỚP MÌNH DẠY VÀ NHÓM PH CỦA LỚP ĐÓ
                    var homeroomIds = await _context.Classes.Where(c => c.LecturerId == user.LecturerId).Select(c => c.Id).ToListAsync();
                    var teachingIds = await _context.Schedules.Where(s => s.LecturerId == user.LecturerId).Select(s => s.ClassId).ToListAsync();
                    var allClassIds = homeroomIds.Union(teachingIds).Distinct();

                    var classes = await _context.Classes.Where(c => allClassIds.Contains(c.Id)).ToListAsync();

                    foreach (var c in classes)
                    {
                        if (!chatRooms.Any(r => r.RoomName == $"Class_{c.Id}"))
                            chatRooms.Add(new ChatRoomViewModel { RoomName = $"Class_{c.Id}", DisplayName = $"🏫 {c.ClassName}", Type = "Class" });

                        if (!chatRooms.Any(r => r.RoomName == $"ParentGroup_{c.Id}"))
                            chatRooms.Add(new ChatRoomViewModel { RoomName = $"ParentGroup_{c.Id}", DisplayName = $"👨‍👩‍👧‍👦 Hội PH {c.ClassName}", Type = "Group" });
                    }
                }
                else if (User.IsInRole("Parent"))
                {
                    string studentCode = user.Username.Replace("PH", "");
                    var stu = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.StudentCode == studentCode);
                    if (stu?.ClassId != null && !chatRooms.Any(r => r.RoomName == $"ParentGroup_{stu.ClassId}"))
                        chatRooms.Add(new ChatRoomViewModel { RoomName = $"ParentGroup_{stu.ClassId}", DisplayName = $"👨‍👩‍👧‍👦 Hội PH {stu.Class.ClassName}", Type = "Group" });
                }
                else if (User.IsInRole("Student"))
                {
                    var stu = await _context.Students.FindAsync(user.StudentId);
                    if (stu?.ClassId != 0 && !chatRooms.Any(r => r.RoomName == $"Class_{stu.ClassId}"))
                        chatRooms.Add(new ChatRoomViewModel { RoomName = $"Class_{stu.ClassId}", DisplayName = $"🏫 Lớp {stu.ClassId}", Type = "Class" });
                }
            }
            return chatRooms;
        }
    }
}