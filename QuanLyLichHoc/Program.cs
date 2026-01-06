using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Data;
using QuanLyLichHoc.Hubs;
using QuanLyLichHoc.Models;
// 1. THÊM 2 THƯ VIỆN NÀY ĐỂ XỬ LÝ FONT TIẾNG VIỆT
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. CẤU HÌNH SERVICES (DEPENDENCY INJECTION)
// ============================================================

// A. Kết nối Database (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// B. Cấu hình Đăng nhập (Cookie Authentication)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });

// C. Đăng ký SignalR
builder.Services.AddSignalR();

// D. Add MVC Controllers with Views (CẤU HÌNH FONT TẠI ĐÂY)
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Giữ nguyên tiếng Việt, không mã hóa thành ký tự unicode (\uXXXX)
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    });

// Cấu hình upload file lớn
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 209715200; // 200 MB
});

builder.Services.AddScoped<QuanLyLichHoc.Services.GeminiService>();

var app = builder.Build();

// ============================================================
// 2. DATA SEEDING
// ============================================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Kiểm tra xem đã có tài khoản nào chưa
        if (!context.AppUsers.Any())
        {
            context.AppUsers.Add(new AppUser
            {
                Username = "admin",
                Password = "123",
                Role = "Admin"
            });
            context.SaveChanges();
            Console.WriteLine("--> Đã tạo tài khoản Admin mặc định: admin / 123");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Đã xảy ra lỗi khi khởi tạo Database.");
    }
}

// ============================================================
// 3. CẤU HÌNH HTTP REQUEST PIPELINE
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/notificationHub");

app.Run();