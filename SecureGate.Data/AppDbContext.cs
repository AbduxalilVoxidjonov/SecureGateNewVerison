using global::SecureGate.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureGate.Domain.Auth;

namespace SecureGate.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Users> Students => Set<Users>();
        public DbSet<Staff> StaffMembers => Set<Staff>();
        public DbSet<Turnstile> Turnstiles => Set<Turnstile>();
        public DbSet<TurnstilePermission> TurnstilePermissions => Set<TurnstilePermission>();
        public DbSet<Camera> Cameras => Set<Camera>();
        public DbSet<CameraGroup> CameraGroups => Set<CameraGroup>();
        public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
        public DbSet<FaceData> FaceData => Set<FaceData>();
        public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
        public DbSet<Alert> Alerts => Set<Alert>();
        public DbSet<Setting> Settings => Set<Setting>();
        public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
        public DbSet<CameraUser> CameraUsers => Set<CameraUser>();
        public DbSet<UserCameraGroup> UserCameraGroups => Set<UserCameraGroup>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraints
            modelBuilder.Entity<Users>()
                .HasIndex(s => s.StudentId)
                .IsUnique();

            modelBuilder.Entity<Setting>()
                .HasIndex(s => s.Key)
                .IsUnique();

            // Kamera kodi (CAM-XX) butun jadval bo'yicha noyob bo'lishi shart —
            // race condition oldini olish uchun (CameraService'da Id'dan hosil qilinadi).
            modelBuilder.Entity<Camera>()
                .HasIndex(c => c.CameraCode)
                .IsUnique();

            // Student -> BlockedUser (one-to-one)
            modelBuilder.Entity<Users>()
                .HasOne(s => s.BlockedUser)
                .WithOne(b => b.Student)
                .HasForeignKey<BlockedUser>(b => b.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            // Cascade delete prevention
            modelBuilder.Entity<AccessLog>()
                .HasOne(a => a.Turnstile)
                .WithMany(t => t.AccessLogs)
                .HasForeignKey(a => a.TurnstileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AccessLog>()
                .HasOne(a => a.Student)
                .WithMany(s => s.AccessLogs)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Turnstile>()
                .HasOne(t => t.LinkedCamera)
                .WithMany(c => c.LinkedTurnstiles)
                .HasForeignKey(t => t.LinkedCameraId)
                .OnDelete(DeleteBehavior.SetNull);

            // Bir foydalanuvchi faqat bitta yuz profiliga ega bo'lishi uchun
            // Student uchun Unique Index (bir talabaga bitta yuz)
            modelBuilder.Entity<FaceData>()
                .HasIndex(f => f.StudentId)
                .IsUnique()
                .HasFilter("[StudentId] IS NOT NULL");

            // Teacher uchun Unique Index
            modelBuilder.Entity<FaceData>()
                .HasIndex(f => f.TeacherId)
                .IsUnique()
                .HasFilter("[TeacherId] IS NOT NULL");

            // UserPermission konfiguratsiyasi
            modelBuilder.Entity<UserPermission>()
                .HasOne(up => up.User)
                .WithMany(u => u.Permissions)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasIndex(up => new { up.UserId, up.Permission })
                .IsUnique();

            // CameraUser konfiguratsiyasi
            modelBuilder.Entity<CameraUser>()
                .HasOne(c => c.Camera)
                .WithMany()
                .HasForeignKey(c => c.CameraId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CameraUser>()
                .HasOne(c => c.Student)
                .WithMany()
                .HasForeignKey(c => c.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CameraUser>()
                .HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CameraUser>()
                .HasOne(c => c.Staff)
                .WithMany()
                .HasForeignKey(c => c.StaffId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CameraUser>()
                .HasIndex(c => c.DetectedAt);

            modelBuilder.Entity<CameraUser>()
                .HasIndex(c => new { c.FirstName, c.LastName });

            // ===== UserCameraGroup (admin → kamera guruhlari biriktirilishi) =====
            modelBuilder.Entity<UserCameraGroup>()
                .HasOne(x => x.User)
                .WithMany(u => u.CameraGroups)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserCameraGroup>()
                .HasOne(x => x.CameraGroup)
                .WithMany()
                .HasForeignKey(x => x.CameraGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir admin uchun bir guruh faqat bir marta biriktiriladi
            modelBuilder.Entity<UserCameraGroup>()
                .HasIndex(x => new { x.UserId, x.CameraGroupId })
                .IsUnique();
        }
    }
}
