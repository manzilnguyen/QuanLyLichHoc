using ClosedXML.Excel; // Thư viện xuất Excel
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Hubs;
using QuanLyLichHoc.Models;
using System.Text.RegularExpressions;

namespace QuanLyLichHoc.Controllers
{
    [Authorize]
    public class TuitionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notiHub;

        public TuitionController(ApplicationDbContext context, IHubContext<NotificationHub> notiHub)
        {
            _context = context;
            _notiHub = notiHub;
        }

        // ============================================================
        // 1. TRANG QUẢN LÝ (ADMIN & GIẢNG VIÊN)
        // ============================================================
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Index(int? classId, string status)
        {
            var query = _context.TuitionFees.Include(t => t.Student).ThenInclude(s => s.Class).AsQueryable();

            // Nếu là Giảng viên -> Chỉ xem lớp mình dạy
            if (User.IsInRole("Lecturer"))
            {
                var lecIdStr = User.FindFirst("LecturerId")?.Value;
                if (lecIdStr != null)
                {
                    int lecId = int.Parse(lecIdStr);
                    var classIds = await _context.Schedules.Where(s => s.LecturerId == lecId).Select(s => s.ClassId).Distinct().ToListAsync();
                    var homeClassIds = await _context.Classes.Where(c => c.LecturerId == lecId).Select(c => c.Id).ToListAsync();
                    var allClasses = classIds.Concat(homeClassIds).Distinct();
                    query = query.Where(t => allClasses.Contains(t.Student.ClassId));
                }
            }

            // Bộ lọc dữ liệu
            if (classId.HasValue) query = query.Where(t => t.Student.ClassId == classId);
            if (!string.IsNullOrEmpty(status))
            {
                bool isPaid = status == "Paid";
                query = query.Where(t => t.IsPaid == isPaid);
            }

            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName", classId);

            var list = await query.OrderByDescending(t => t.Id).ToListAsync();

            // Thống kê cho View
            ViewBag.TotalAmount = list.Sum(t => t.Amount);
            ViewBag.PaidCount = list.Count(t => t.IsPaid);
            ViewBag.UnpaidCount = list.Count(t => !t.IsPaid);

            return View(list);
        }

        // ============================================================
        // 2. TẠO KHOẢN THU (ADMIN)
        // ============================================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["ClassId"] = new SelectList(_context.Classes, "Id", "ClassName");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(int classId, string title, decimal amount, string semester)
        {
            var students = await _context.Students.Include(s => s.AppUser).Where(s => s.ClassId == classId).ToListAsync();
            if (!students.Any()) return BadRequest("Lớp không có sinh viên");

            var fees = new List<TuitionFee>();
            var notifications = new List<Notification>();

            foreach (var s in students)
            {
                bool exists = await _context.TuitionFees.AnyAsync(t => t.StudentId == s.Id && t.Semester == semester && t.Title == title);
                if (!exists)
                {
                    fees.Add(new TuitionFee
                    {
                        StudentId = s.Id,
                        Title = title,
                        Amount = amount,
                        Semester = semester,
                        IsPaid = false
                    });

                    // Tạo thông báo
                    if (s.AppUser != null)
                    {
                        var noti = new Notification
                        {
                            UserId = s.AppUser.Id,
                            Title = "💰 Học phí mới",
                            Message = $"Vui lòng đóng: {title} ({amount:N0}đ)",
                            Url = "/Tuition/MyTuition",
                            Type = "Warning",
                            CreatedAt = DateTime.Now
                        };
                        notifications.Add(noti);
                        await _notiHub.Clients.User(s.AppUser.Username).SendAsync("ReceiveNotification", noti.Title, noti.Message, noti.Url, noti.Type);
                    }
                }
            }

            if (fees.Any())
            {
                _context.TuitionFees.AddRange(fees);
                if (notifications.Any()) _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Đã tạo khoản thu cho {fees.Count} sinh viên.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 3. XÁC NHẬN THANH TOÁN THỦ CÔNG
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var fee = await _context.TuitionFees.Include(t => t.Student.AppUser).FirstOrDefaultAsync(t => t.Id == id);
            if (fee != null)
            {
                fee.IsPaid = true;
                fee.PaymentDate = DateTime.Now;

                // Gửi thông báo
                if (fee.Student?.AppUser != null)
                {
                    var noti = new Notification
                    {
                        UserId = fee.Student.AppUser.Id,
                        Title = "✅ Thanh toán thành công",
                        Message = $"Đã xác nhận khoản: {fee.Title}",
                        Url = "/Tuition/PaymentHistory",
                        Type = "Success",
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(noti);
                    await _notiHub.Clients.User(fee.Student.AppUser.Username).SendAsync("ReceiveNotification", noti.Title, noti.Message, noti.Url, noti.Type);
                }

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 4. GỬI NHẮC NHỞ ĐÓNG TIỀN
        // ============================================================
        [HttpPost]
        [Authorize(Roles = "Admin,Lecturer")]
        public async Task<IActionResult> Remind(int id)
        {
            var fee = await _context.TuitionFees.Include(t => t.Student.AppUser).FirstOrDefaultAsync(t => t.Id == id);
            if (fee != null && !fee.IsPaid && fee.Student.AppUser != null)
            {
                string msg = $"Bạn chưa đóng học phí: {fee.Title} ({fee.Amount:N0}đ). Vui lòng thanh toán ngay.";

                // A. Gửi Sinh viên
                var notiSV = new Notification
                {
                    UserId = fee.Student.AppUser.Id,
                    Title = "⚠️ Nhắc nhở học phí",
                    Message = msg,
                    Url = "/Tuition/MyTuition",
                    Type = "Danger",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notiSV);
                await _notiHub.Clients.User(fee.Student.AppUser.Username).SendAsync("ReceiveNotification", notiSV.Title, notiSV.Message, notiSV.Url, notiSV.Type);

                // B. Gửi Phụ huynh
                string parentUsername = fee.Student.StudentCode + "PH";
                var parentUser = await _context.AppUsers.FirstOrDefaultAsync(u => u.Username == parentUsername);
                if (parentUser != null)
                {
                    var notiPH = new Notification
                    {
                        UserId = parentUser.Id,
                        Title = "⚠️ Nhắc nhở học phí",
                        Message = $"Con bạn chưa đóng: {fee.Title}",
                        Url = "/Tuition/MyTuition",
                        Type = "Danger",
                        CreatedAt = DateTime.Now
                    };
                    _context.Notifications.Add(notiPH);
                    await _notiHub.Clients.User(parentUsername).SendAsync("ReceiveNotification", notiPH.Title, notiPH.Message, notiPH.Url, notiPH.Type);
                }

                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        // ============================================================
        // 5. VIEW HỌC SINH / PHỤ HUYNH
        // ============================================================
        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> MyTuition()
        {
            string studentCode = User.Identity.Name.Replace("PH", "");
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode);
            if (student == null) return NotFound();

            var fees = await _context.TuitionFees.Where(t => t.StudentId == student.Id).OrderByDescending(t => t.CreatedAt).ToListAsync();
            return View(fees);
        }

        [Authorize(Roles = "Student,Parent")]
        public async Task<IActionResult> PaymentHistory()
        {
            string studentCode = User.Identity.Name.Replace("PH", "");
            var student = await _context.Students.Include(s => s.Class).FirstOrDefaultAsync(s => s.StudentCode == studentCode);
            if (student == null) return NotFound();

            var paidFees = await _context.TuitionFees.Where(t => t.StudentId == student.Id && t.IsPaid).OrderByDescending(t => t.PaymentDate).ToListAsync();
            ViewBag.Student = student;
            ViewBag.TotalPaid = paidFees.Sum(x => x.Amount);
            ViewBag.TotalTransactions = paidFees.Count;

            return View(paidFees);
        }

        // ============================================================
        // 6. [NÂNG CAO] XUẤT EXCEL
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportToExcel(int? classId, string status)
        {
            var query = _context.TuitionFees.Include(t => t.Student).ThenInclude(s => s.Class).AsQueryable();
            if (classId.HasValue) query = query.Where(t => t.Student.ClassId == classId);
            if (!string.IsNullOrEmpty(status)) { bool isPaid = status == "Paid"; query = query.Where(t => t.IsPaid == isPaid); }
            var data = await query.OrderByDescending(t => t.Id).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("HocPhi");
                // Header
                worksheet.Cell(1, 1).Value = "Mã GD";
                worksheet.Cell(1, 2).Value = "Mã SV";
                worksheet.Cell(1, 3).Value = "Họ Tên";
                worksheet.Cell(1, 4).Value = "Lớp";
                worksheet.Cell(1, 5).Value = "Nội dung";
                worksheet.Cell(1, 6).Value = "Số tiền";
                worksheet.Cell(1, 7).Value = "Trạng thái";
                worksheet.Cell(1, 8).Value = "Ngày đóng";

                var header = worksheet.Range("A1:H1");
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.CornflowerBlue;
                header.Style.Font.FontColor = XLColor.White;

                int row = 2;
                foreach (var item in data)
                {
                    worksheet.Cell(row, 1).Value = item.Id;
                    worksheet.Cell(row, 2).Value = item.Student.StudentCode;
                    worksheet.Cell(row, 3).Value = item.Student.FullName;
                    worksheet.Cell(row, 4).Value = item.Student.Class?.ClassName;
                    worksheet.Cell(row, 5).Value = item.Title;
                    worksheet.Cell(row, 6).Value = item.Amount;
                    worksheet.Cell(row, 7).Value = item.IsPaid ? "Đã đóng" : "Chưa đóng";
                    worksheet.Cell(row, 8).Value = item.PaymentDate?.ToString("dd/MM/yyyy HH:mm");

                    if (!item.IsPaid) worksheet.Range($"A{row}:H{row}").Style.Font.FontColor = XLColor.Red;
                    row++;
                }
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HocPhi_{DateTime.Now:ddMMyyyy}.xlsx");
                }
            }
        }

        // ============================================================
        // 7. WEBHOOK TỰ ĐỘNG DUYỆT (SePay)
        // ============================================================
        [HttpPost]
        [Route("api/sepay/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookModel data)
        {
            if (data == null || string.IsNullOrEmpty(data.Content)) return BadRequest();

            // Regex tìm HP123
            var match = Regex.Match(data.Content, @"HP(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int feeId))
            {
                var fee = await _context.TuitionFees.Include(t => t.Student.AppUser).FirstOrDefaultAsync(t => t.Id == feeId);

                // Kiểm tra hợp lệ và số tiền
                if (fee != null && !fee.IsPaid && data.TransferAmount >= fee.Amount)
                {
                    fee.IsPaid = true;
                    fee.PaymentDate = DateTime.Now;

                    // Thông báo thành công
                    if (fee.Student?.AppUser != null)
                    {
                        var noti = new Notification
                        {
                            UserId = fee.Student.AppUser.Id,
                            Title = "✅ Thanh toán thành công (Auto)",
                            Message = $"Đã nhận {data.TransferAmount:N0}đ. Cảm ơn bạn!",
                            Url = "/Tuition/PaymentHistory",
                            Type = "Success",
                            CreatedAt = DateTime.Now
                        };
                        _context.Notifications.Add(noti);
                        await _notiHub.Clients.User(fee.Student.AppUser.Username).SendAsync("ReceiveNotification", noti.Title, noti.Message, noti.Url, noti.Type);
                    }

                    await _context.SaveChangesAsync();
                    return Ok(new { success = true });
                }
            }
            return Ok(new { success = false });
        }
    }
}