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
        public DashboardService(AppDbContext db) => _db = db;

        public async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            // AccessLog.Timestamp lokal vaqtda saqlanadi, shuning uchun "bugun" ham lokal
            var today = DateTime.Now.Date;

            return new DashboardViewModel
            {
                ActiveStudentCount = await _db.Students.CountAsync(s => s.Status == StudentStatus.Active),
                TodayPassCount = await _db.AccessLogs.CountAsync(a => a.Timestamp >= today && a.Result == AccessResult.Granted),
                ActiveCameraCount = await _db.Cameras.CountAsync(c => c.Status == CameraStatus.Online),
                TotalCameraCount = await _db.Cameras.CountAsync(),
                AlertCount = await _db.Alerts.CountAsync(),
                NewAlertCount = await _db.Alerts.CountAsync(a => !a.IsRead),
                HourlyPassData = Enumerable.Range(0, 24).Select(h =>
                    _db.AccessLogs.Count(a => a.Timestamp >= today && a.Timestamp.Hour == h && a.Result == AccessResult.Granted)
                ).ToList(),
                RecentActivity = await _db.AccessLogs
                    .Include(a => a.Student).Include(a => a.Teacher).Include(a => a.Staff).Include(a => a.Turnstile)
                    .Where(a => a.Timestamp >= today)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(10)
                    .Select(a => new AccessLogItemViewModel
                    {
                        UserName = a.Student != null ? a.Student.FirstName + " " + a.Student.LastName :
                                   a.Teacher != null ? a.Teacher.FullName :
                                   a.Staff != null ? a.Staff.FullName : "Noma'lum",
                        Action = a.Turnstile != null ? a.Turnstile.Name + " dan " + (a.Result == AccessResult.Granted ? "o'tdi" : "rad etildi") : "Noma'lum yuz",
                        Time = a.Timestamp.ToString("HH:mm:ss"),
                        Type = a.Result == AccessResult.Granted ? "good" : a.Result == AccessResult.Denied ? "deny" : "warn"
                    }).ToListAsync(),
                PopularTurnstiles = await _db.Turnstiles
                    .Where(t => t.Status == TurnstileStatus.Online)
                    .OrderByDescending(t => t.TodayPassCount)
                    .Take(5)
                    .Select(t => new TurnstileStatViewModel
                    {
                        Name = t.Name,
                        Count = t.TodayPassCount,
                        Percentage = t.TodayPassCount > 0 ? (int)((double)t.TodayPassCount / 500 * 100) : 0
                    }).ToListAsync(),
                RecentAlerts = await _db.Alerts.OrderByDescending(a => a.CreatedAt).Take(5).ToListAsync()
            };
        }
    }
}
