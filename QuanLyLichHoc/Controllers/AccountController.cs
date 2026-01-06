using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Models;
using System.Security.Claims;

namespace QuanLyLichHoc.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            // Nếu đã đăng nhập rồi thì chuyển hướng luôn, không hiện form login nữa
            if (User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Index", "Home");
                if (User.IsInRole("Parent")) return RedirectToAction("Index", "Home");
                if (User.IsInRole("Student")) return RedirectToAction("Index", "Home");
                if (User.IsInRole("Lecturer")) return RedirectToAction("Index", "Home");
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // 1. Tìm User trong database
            var user = await _context.AppUsers
                .Include(u => u.Student) // Load kèm thông tin HS (nếu có)
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                // 2. (MỚI) Kiểm tra tài khoản có bị khóa không
                if (!user.IsActive)
                {
                    ViewBag.Error = "Tài khoản này đã bị khóa. Vui lòng liên hệ Admin.";
                    return View();
                }

                // 3. Tạo Claims (Thông tin định danh)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("UserId", user.Id.ToString()) // Lưu ID để dùng cho Chat/UserManage
                };

                // --- Ghi nhớ ID riêng cho GV/HS ---
                if (user.Role == "Lecturer" && user.LecturerId != null)
                {
                    claims.Add(new Claim("LecturerId", user.LecturerId.ToString()));
                }
                else if (user.Role == "Student" && user.StudentId != null)
                {
                    claims.Add(new Claim("StudentId", user.StudentId.ToString()));
                    // Lưu ClassId để tiện tra lịch
                    if (user.Student.ClassId != null)
                    {
                        claims.Add(new Claim("ClassId", user.Student.ClassId.ToString()));
                    }
                }
                // ----------------------------------

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true, // Ghi nhớ đăng nhập (Keep me signed in)
                    ExpiresUtc = DateTime.UtcNow.AddHours(2)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                // 4. (QUAN TRỌNG) ĐIỀU HƯỚNG THEO ROLE
                if (user.Role == "Admin")
                {
                    // Admin vào trang quản lý tài khoản hoặc trang chủ
                    return RedirectToAction("Index", "Home");
                }
                else if (user.Role == "Parent")
                {
                    // ===> DÒNG NÀY QUAN TRỌNG NHẤT VỚI BẠN <===
                    // Phụ huynh vào Dashboard riêng
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // GV và HS vào xem lịch
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
            return View();
        }

        // GET: Đổi mật khẩu
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: Xử lý đổi mật khẩu
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var username = User.Identity.Name;
            var user = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null) return RedirectToAction("Login");

            // Kiểm tra mật khẩu cũ
            if (user.Password != model.CurrentPassword)
            {
                ModelState.AddModelError("CurrentPassword", "Mật khẩu hiện tại không đúng");
                return View(model);
            }

            // Cập nhật mật khẩu mới
            user.Password = model.NewPassword;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công!";

            // Đổi xong logout ra để đăng nhập lại cho an toàn
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return Content("Bạn không có quyền truy cập trang này!");
        }
    }
}