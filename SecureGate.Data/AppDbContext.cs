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
        public DbSet<Teacher> Teachers => Set<Teacher>();
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

            // DbSet<Teacher> qo'shilgani jadval nomini "Teachers"ga o'zgartirib yubormasligi
            // uchun mavjud "Teacher" nomi aniq belgilanadi (rename migratsiyasi kerak emas).
            modelBuilder.Entity<Teacher>().ToTable("Teacher");

            // Unique constraints
            // StudentId ixtiyoriy: NULL'lar indeksdan chiqarib tashlanadi, shuning uchun
            // ID raqami berilmagan bir nechta o'quvchi bir vaqtda mavjud bo'la oladi.
            modelBuilder.Entity<Users>()
                .HasIndex(s => s.StudentId)
                .IsUnique()
                .HasFilter("[StudentId] IS NOT NULL");

            modelBuilder.Entity<Setting>()
                .HasIndex(s => s.Key)
                .IsUnique();

            // Kamera kodi (CAM-XX) butun jadval bo'yicha noyob bo'lishi shart —
            // race condition oldini olish uchun (CameraService'da Id'dan hosil qilinadi).
            modelBuilder.Entity<Camera>()
                .HasIndex(c => c.CameraCode)
                .IsUnique();

            // Student -> BlockedUser (one-to-one).
            // Blok yozuvi shaxsdan ajralgan holda ma'nosiz (joriy holat muhim, tarix emas),
            // shuning uchun o'quvchi o'chirilsa blok yozuvi ham o'chadi — yetim qator qolmaydi.
            modelBuilder.Entity<Users>()
                .HasOne(s => s.BlockedUser)
                .WithOne(b => b.Student)
                .HasForeignKey<BlockedUser>(b => b.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

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

            // Log tarixi saqlanib qoladi, ammo Teacher/Staff/Camera o'chirilishi
            // RESTRICT bilan bloklanmasligi kerak.
            modelBuilder.Entity<AccessLog>()
                .HasOne(a => a.Teacher)
                .WithMany(t => t.AccessLogs)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AccessLog>()
                .HasOne(a => a.Staff)
                .WithMany(s => s.AccessLogs)
                .HasForeignKey(a => a.StaffId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AccessLog>()
                .HasOne(a => a.Camera)
                .WithMany()
                .HasForeignKey(a => a.CameraId)
                .OnDelete(DeleteBehavior.SetNull);

            // Dashboard/hisobotlar aynan shu ustunlar bo'yicha filtrlaydi.
            modelBuilder.Entity<AccessLog>()
                .HasIndex(a => new { a.Timestamp, a.Result });

            // Bildirishnomalar ro'yxati: o'qilmaganlar + sana bo'yicha tartib.
            modelBuilder.Entity<Alert>()
                .HasIndex(a => new { a.IsRead, a.CreatedAt });

            // ===== TurnstilePermission: ruxsat yozuvi shaxssiz ma'nosiz =====
            modelBuilder.Entity<TurnstilePermission>()
                .HasOne(p => p.Student)
                .WithMany(s => s.TurnstilePermissions)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TurnstilePermission>()
                .HasOne(p => p.Teacher)
                .WithMany(t => t.TurnstilePermissions)
                .HasForeignKey(p => p.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TurnstilePermission>()
                .HasOne(p => p.Staff)
                .WithMany(s => s.TurnstilePermissions)
                .HasForeignKey(p => p.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== FaceData: yuz profili ham shaxssiz ma'nosiz =====
            modelBuilder.Entity<FaceData>()
                .HasOne(f => f.Student)
                .WithMany(s => s.FaceDataList)
                .HasForeignKey(f => f.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FaceData>()
                .HasOne(f => f.Teacher)
                .WithMany()
                .HasForeignKey(f => f.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FaceData>()
                .HasOne(f => f.Staff)
                .WithMany()
                .HasForeignKey(f => f.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            // Admin akkaunt xodimga bog'langan bo'lsa ham, xodim o'chirilishi
            // akkauntni bloklamasin — bog'lanish uziladi.
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Staff)
                .WithMany()
                .HasForeignKey(u => u.StaffId)
                .OnDelete(DeleteBehavior.SetNull);

            // Guruh o'chirilsa kamera guruhsiz qoladi (UserCameraGroup bilan izchil xatti-harakat).
            modelBuilder.Entity<Camera>()
                .HasOne(c => c.CameraGroup)
                .WithMany(g => g.Cameras)
                .HasForeignKey(c => c.CameraGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            // ===== Turniket va kamera identifikatorlari noyob =====
            modelBuilder.Entity<Turnstile>()
                .HasIndex(t => t.Name)
                .IsUnique();

            modelBuilder.Entity<Turnstile>()
                .HasIndex(t => new { t.IpAddress, t.Port })
                .IsUnique()
                .HasFilter("[IpAddress] IS NOT NULL");

            // Oddiy IP-kameralar uchun IP:Port juftligi noyob bo'lishi shart.
            // NVR kanallari bundan mustasno: bitta NVR (bir xil IP:Port) ortida
            // o'nlab kanal turadi, shuning uchun filtr faqat DeviceKind = 0 (Camera) ga tegishli.
            modelBuilder.Entity<Camera>()
                .HasIndex(c => new { c.IpAddress, c.Port })
                .IsUnique()
                .HasFilter("[IpAddress] IS NOT NULL AND [DeviceKind] = 0");

            // Bitta NVR da bitta kanal faqat bir marta ro'yxatga olinadi.
            modelBuilder.Entity<Camera>()
                .HasIndex(c => new { c.IpAddress, c.Port, c.ChannelNumber })
                .IsUnique()
                .HasFilter("[IpAddress] IS NOT NULL AND [ChannelNumber] IS NOT NULL");

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

            // Staff uchun Unique Index — "bir shaxsga bitta yuz profili" qoidasi
            // uchala shaxs turi uchun ham bir xil amal qiladi.
            modelBuilder.Entity<FaceData>()
                .HasIndex(f => f.StaffId)
                .IsUnique()
                .HasFilter("[StaffId] IS NOT NULL");

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
