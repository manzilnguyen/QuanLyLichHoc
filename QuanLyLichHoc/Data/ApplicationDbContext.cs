using Microsoft.EntityFrameworkCore;
using QuanLyLichHoc.Models;

namespace QuanLyLichHoc.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Lecturer> Lecturers { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Schedule> Schedules { get; set; }

        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<ChatRoomInfo> ChatRoomInfos { get; set; }
        public DbSet<ChatRoomMember> ChatRoomMembers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<SystemNotification> SystemNotifications { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LecturerEvaluation> LecturerEvaluations { get; set; }
        public DbSet<TuitionFee> TuitionFees { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Attendance>().HasOne(a => a.Student).WithMany().OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Attendance>().HasOne(a => a.Schedule).WithMany().OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Enrollment>().HasOne(e => e.Student).WithMany().OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Enrollment>().HasOne(e => e.Class).WithMany().OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Student).WithMany().HasForeignKey(l => l.StudentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Class).WithMany().HasForeignKey(l => l.ClassId).OnDelete(DeleteBehavior.NoAction);

            // --- CẤU HÌNH QUAN HỆ 1-1 (SỬA LỖI) ---
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Lecturer)
                .WithOne(l => l.AppUser)
                .HasForeignKey<AppUser>(u => u.LecturerId);

            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Student)
                .WithOne(s => s.AppUser)
                .HasForeignKey<AppUser>(u => u.StudentId);
        }
    }
}