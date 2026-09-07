using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public DashboardService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            // AccessLog.Timestamp DB'da UTC saqlanadi. "Bugun" tushunchasi esa mahalliy —
            // shuning uchun mahalliy kun chegaralarini hisoblab, UTC'ga o'giramiz.
            // Config: App:LocalUtcOffsetHours (default 5 = Asia/Tashkent).
            var offsetHours = _config.GetValue<double?>("App:LocalUtcOffsetHours") ?? 5;
            var offset = TimeSpan.FromHours(offsetHours);

            var localNow = DateTime.UtcNow + offset;
            var fromUtc = localNow.Date - offset;          // mahalliy bugun 00:00 → UTC
            var toUtc = fromUtc.AddDays(1);                // mahalliy ertaga 00:00 → UTC

            // Soatlik statistika — 24 ta alohida sinxron Count o'rniga BITTA async GroupBy.
            var hourlyRows = await _db.AccessLogs
                .Where(a => a.Timestamp >= fromUtc && a.Timestamp < toUtc && a.Result == AccessResult.Granted)
                .GroupBy(a => a.Timestamp.AddHours(offsetHours).Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            var hourlyPassData = new List<int>(new int[24]);
            foreach (var row in hourlyRows)
            {
                if (row.Hour >= 0 && row.Hour < 24)
                    hourlyPassData[row.Hour] = row.Count;
            }

            var popular = await _db.Turnstiles
                .Where(t => t.Status == TurnstileStatus.Online)
                .OrderByDescending(t => t.TodayPassCount)
                .Take(5)
                .Select(t => new { t.Name, t.TodayPassCount })
                .ToListAsync();

            // Foiz endi qattiq kodlangan 500 ga emas, ro'yxatdagi MAKSIMUM qiymatga nisbatan.
            var maxPass = popular.Count > 0 ? popular.Max(p => p.TodayPassCount) : 0;
            if (maxPass <= 0) maxPass = 1;

            // ToString(format) SQL'ga tarjima qilinmaydi — avval maydonlarni olamiz,
            // formatlashni xotirada bajaramiz.
            var recentRows = await _db.AccessLogs
                .Where(a => a.Timestamp >= fromUtc && a.Timestamp < toUtc)
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new
                {
                    StudentName = a.Student != null ? a.Student.FirstName + " " + a.Student.LastName : null,
                    TeacherName = a.Teacher != null ? a.Teacher.FullName : null,
                    StaffName = a.Staff != null ? a.Staff.FullName : null,
                    TurnstileName = a.Turnstile != null ? a.Turnstile.Name : null,
                    a.Timestamp,
                    a.Result
                })
                .ToListAsync();

            var recentActivity = recentRows.Select(a => new AccessLogItemViewModel
            {
                UserName = a.StudentName ?? a.TeacherName ?? a.StaffName ?? "Noma'lum",
                Action = a.TurnstileName != null
                    ? a.TurnstileName + " dan " + (a.Result == AccessResult.Granted ? "o'tdi" : "rad etildi")
                    : "Noma'lum yuz",
                // ISO-8601 UTC — mahalliy vaqtga o'girish va formatlash frontend ishi.
                Time = a.Timestamp.ToString("O"),
                Type = a.Result == AccessResult.Granted ? "good" : a.Result == AccessResult.Denied ? "deny" : "warn"
            }).ToList();

            return new DashboardViewModel
            {
                ActiveStudentCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active),
                TodayPassCount = await _db.AccessLogs.CountAsync(a =>
                    a.Timestamp >= fromUtc && a.Timestamp < toUtc && a.Result == AccessResult.Granted),
                ActiveCameraCount = await _db.Cameras.CountAsync(c => c.Status == CameraStatus.Online),
                TotalCameraCount = await _db.Cameras.CountAsync(),
                AlertCount = await _db.Alerts.CountAsync(),
                NewAlertCount = await _db.Alerts.CountAsync(a => !a.IsRead),
                HourlyPassData = hourlyPassData,
                RecentActivity = recentActivity,
                PopularTurnstiles = popular
                    .Select(t => new TurnstileStatViewModel
                    {
                        Name = t.Name,
                        Count = t.TodayPassCount,
                        Percentage = Math.Clamp((int)Math.Round(t.TodayPassCount / (double)maxPass * 100), 0, 100)
                    }).ToList(),
                RecentAlerts = await _db.Alerts.OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync()
            };
        }
    }
}
