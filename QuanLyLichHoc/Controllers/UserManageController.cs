using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Controllers
{
    [Authorize(Roles = "Admin")] // Chỉ Admin mới được vào
    public class UserManageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserManageController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. DANH SÁCH (INDEX)
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var users = await _context.AppUsers
                .Include(u => u.Lecturer)
                .Include(u => u.Student)
                .OrderByDescending(u => u.Id) // Mới nhất lên đầu
                .ToListAsync();
            return View(users);
        }

        // ============================================================
        // 2. AUTO CẤP TÀI KHOẢN (GIẢNG VIÊN & HỌC SINH)
        // ============================================================
        // Hàm này xử lý nút "Auto Cấp" màu vàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoGenerateAccounts()
        {
            int countLecturer = 0;
            int countStudent = 0;

            // A. CẤP CHO GIẢNG VIÊN (Username = Email)
            var lecturers = await _context.Lecturers.ToListAsync();
            foreach (var item in lecturers)
            {
                // Bỏ qua nếu không có email hoặc email đã được dùng làm username
                if (string.IsNullOrEmpty(item.Email)) continue;

                // Kiểm tra xem email này đã có tài khoản chưa
                if (!_context.AppUsers.Any(u => u.Username == item.Email))
                {
                    _context.AppUsers.Add(new AppUser
                    {
                        Username = item.Email,
                        Password = "123",      // Mật khẩu mặc định
                        Role = "Lecturer",
                        LecturerId = item.Id,
                        IsActive = true        // Kích hoạt ngay
                    });
                    countLecturer++;
                }
            }

            // B. CẤP CHO HỌC SINH (Username = Mã SV)
            var students = await _context.Students.ToListAsync();
            foreach (var item in students)
            {
                // Bỏ qua nếu không có mã SV
                if (string.IsNullOrEmpty(item.StudentCode)) continue;

                // Kiểm tra xem mã SV này đã có tài khoản chưa
                if (!_context.AppUsers.Any(u => u.Username == item.StudentCode))
                {
                    _context.AppUsers.Add(new AppUser
                    {
                        Username = item.StudentCode,
                        Password = "123",           // Mật khẩu mặc định
                        Role = "Student",
                        StudentId = item.Id,
                        IsActive = true             // Kích hoạt ngay
                    });
                    countStudent++;
                }
            }

            // Lưu thay đổi vào Database
            await _context.SaveChangesAsync();

            // Thông báo kết quả
            if (countLecturer > 0 || countStudent > 0)
            {
                TempData["Success"] = $"Đã cấp mới: {countLecturer} TK Giảng viên & {countStudent} TK Học sinh.";
            }
            else
            {
                // Dòng này sửa lỗi hiển thị loằng ngoằng trong ảnh của bạn
                TempData["Info"] = "Tất cả mọi người đều đã có tài khoản. Không cần cấp thêm.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 3. AUTO CẤP TÀI KHOẢN PHỤ HUYNH
        // ============================================================
        public async Task<IActionResult> AutoCreateParents()
        {
            // Lấy tất cả tài khoản Học sinh hiện có
            var students = await _context.AppUsers
                .Where(u => u.Role == "Student")
                .ToListAsync();

            int count = 0;

            foreach (var st in students)
            {
                // Quy tắc: TK con là "abc" -> TK phụ huynh là "abcPH"
                string parentUsername = st.Username + "PH";

                // Kiểm tra trùng (đã có tk phụ huynh chưa)
                bool exists = await _context.AppUsers.AnyAsync(u => u.Username == parentUsername);

                if (!exists)
                {
                    var newParent = new AppUser
                    {
                        Username = parentUsername,
                        Password = "123",
                        Role = "Parent",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                        // Không cần StudentId, logic tìm con sẽ dựa vào Username
                    };

                    _context.AppUsers.Add(newParent);
                    count++;
                }
            }

            if (count > 0)
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Thành công! Đã tạo mới {count} tài khoản Phụ huynh.";
            }
            else
            {
                TempData["Info"] = "Tất cả học sinh đều đã có tài khoản Phụ huynh.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4. TẠO THỦ CÔNG - GIẢNG VIÊN
        // ============================================================
        public IActionResult CreateLecturerAccount()
        {
            var takenIds = _context.AppUsers.Where(u => u.LecturerId != null).Select(u => u.LecturerId).ToList();
            var availableLecs = _context.Lecturers.Where(l => !takenIds.Contains(l.Id)).ToList();

            if (!availableLecs.Any())
            {
                TempData["Warning"] = "Tất cả giảng viên đều đã có tài khoản!";
                return RedirectToAction("Index");
            }

            ViewData["LecturerId"] = new SelectList(availableLecs, "Id", "FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLecturerAccount(string username, string password, int lecturerId)
        {
            if (_context.AppUsers.Any(u => u.Username == username))
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại!");
                return CreateLecturerAccount(); // Gọi lại hàm GET để load lại dropdown
            }

            var user = new AppUser
            {
                Username = username,
                Password = password,
                Role = "Lecturer",
                LecturerId = lecturerId,
                IsActive = true
            };

            _context.Add(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Tạo tài khoản Giảng viên thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 5. TẠO THỦ CÔNG - HỌC SINH
        // ============================================================
        public IActionResult CreateStudentAccount()
        {
            var takenIds = _context.AppUsers.Where(u => u.StudentId != null).Select(u => u.StudentId).ToList();
            var availableStuds = _context.Students
                .Where(s => !takenIds.Contains(s.Id))
                .Include(s => s.Class)
                .ToList();

            if (!availableStuds.Any())
            {
                TempData["Warning"] = "Tất cả học sinh đều đã có tài khoản!";
                return RedirectToAction("Index");
            }

            var selectList = availableStuds.Select(s => new {
                Id = s.Id,
                DisplayName = $"{s.FullName} ({s.Class?.ClassName})"
            });

            ViewData["StudentId"] = new SelectList(selectList, "Id", "DisplayName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStudentAccount(string username, string password, int studentId)
        {
            if (_context.AppUsers.Any(u => u.Username == username))
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại!");
                return CreateStudentAccount();
            }

            var user = new AppUser
            {
                Username = username,
                Password = password,
                Role = "Student",
                StudentId = studentId,
                IsActive = true
            };

            _context.Add(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Tạo tài khoản Học sinh thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 6. XÓA TÀI KHOẢN
        // ============================================================
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.AppUsers.FindAsync(id);
            if (user != null)
            {
                _context.AppUsers.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài khoản.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}