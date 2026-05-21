using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _db;
        public ReportService(AppDbContext db) => _db = db;

        public async Task<ReportViewModel> GetReportDataAsync()
        {
            // AccessLog.Timestamp lokal vaqtda saqlanadi, shuning uchun haftalik filtrlar ham lokal
            var now = DateTime.Now;
            var weekStart = now.Date.AddDays(-(int)now.DayOfWeek + 1);
            return new ReportViewModel
            {
                WeeklyPassCount = await _db.AccessLogs.CountAsync(a => a.Timestamp >= weekStart && a.Result == AccessResult.Granted),
                AverageAttendance = 94,
                LateArrivals = 63,
                DeniedCount = await _db.AccessLogs.CountAsync(a => a.Timestamp >= weekStart && a.Result == AccessResult.Denied),
                WeeklyData = Enumerable.Range(0, 7).Select(d =>
                    _db.AccessLogs.Count(a => a.Timestamp.Date == weekStart.AddDays(d) && a.Result == AccessResult.Granted)
                ).ToList(),
               
            };
        }
    }
}
