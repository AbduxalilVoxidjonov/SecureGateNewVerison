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
        private readonly IConfiguration _config;

        public ReportService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<ReportViewModel> GetReportDataAsync()
        {
            // AccessLog.Timestamp DB'da UTC saqlanadi; "hafta", "kun" va "kech qolish"
            // tushunchalari esa mahalliy — shuning uchun offset qo'llaymiz.
            // Config: App:LocalUtcOffsetHours (default 5 = Asia/Tashkent),
            //         Reports:LateAfterHour   (default 9 — shu soatdan keyin kelgan = kech qolgan).
            var offsetHours = _config.GetValue<double?>("App:LocalUtcOffsetHours") ?? 5;
            var lateAfterHour = _config.GetValue<int?>("Reports:LateAfterHour") ?? 9;

            var offset = TimeSpan.FromHours(offsetHours);
            var localNow = DateTime.UtcNow + offset;

            // Dushanbadan boshlanadigan hafta (mahalliy)
            var dayIndex = ((int)localNow.DayOfWeek + 6) % 7;   // Dushanba = 0
            var weekStartLocal = localNow.Date.AddDays(-dayIndex);
            var weekStartUtc = weekStartLocal - offset;
            var weekEndUtc = weekStartUtc.AddDays(7);

            var weekQuery = _db.AccessLogs.Where(a => a.Timestamp >= weekStartUtc && a.Timestamp < weekEndUtc);

            // Kunlik o'tishlar — 7 ta alohida sinqron Count o'rniga BITTA async GroupBy.
            // `a.Timestamp.Date == day` (non-sargable) o'rniga oraliq filtri ishlatilgan.
            var dailyRows = await weekQuery
                .Where(a => a.Result == AccessResult.Granted)
                .GroupBy(a => a.Timestamp.AddHours(offsetHours).Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var weeklyData = new List<int>(new int[7]);
            foreach (var row in dailyRows)
            {
                var idx = (int)(row.Day - weekStartLocal).TotalDays;
                if (idx >= 0 && idx < 7) weeklyData[idx] = row.Count;
            }

            var weeklyPassCount = weeklyData.Sum();
            var deniedCount = await weekQuery.CountAsync(a => a.Result == AccessResult.Denied);

            // Kech qolganlar — belgilangan soatdan KEYIN kelgan muvaffaqiyatli o'tishlar.
            var lateArrivals = await weekQuery
                .Where(a => a.Result == AccessResult.Granted
                            && a.Timestamp.AddHours(offsetHours).Hour >= lateAfterHour)
                .CountAsync();

            // O'rtacha davomat = (kunlik noyob o'tganlar) / (jami faol shaxslar) × 100,
            // hafta boshidan bugungacha bo'lgan kunlar bo'yicha o'rtacha.
            var averageAttendance = await CalculateAverageAttendanceAsync(
                weekStartUtc, weekEndUtc, offsetHours, weekStartLocal, localNow.Date);

            return new ReportViewModel
            {
                WeeklyPassCount = weeklyPassCount,
                AverageAttendance = averageAttendance,
                LateArrivals = lateArrivals,
                DeniedCount = deniedCount,
                WeeklyData = weeklyData
            };
        }

        private async Task<double> CalculateAverageAttendanceAsync(
            DateTime weekStartUtc, DateTime weekEndUtc, double offsetHours,
            DateTime weekStartLocal, DateTime todayLocal)
        {
            var totalActive =
                await _db.Students.CountAsync(s => s.Status == StudentStatus.Active)
                + await _db.StaffMembers.CountAsync(s => s.Status == StaffStatus.Active)
                + await _db.Teachers.CountAsync(t => t.Status == TeacherStatus.Active);

            if (totalActive == 0) return 0;

            var granted = _db.AccessLogs.Where(a =>
                a.Timestamp >= weekStartUtc && a.Timestamp < weekEndUtc && a.Result == AccessResult.Granted);

            // (kun, shaxs) juftliklari — har bir shaxs turi uchun alohida, distinct bilan.
            var studentDays = await granted
                .Where(a => a.StudentId != null)
                .Select(a => new { Day = a.Timestamp.AddHours(offsetHours).Date, Id = a.StudentId!.Value })
                .Distinct()
                .ToListAsync();

            var staffDays = await granted
                .Where(a => a.StaffId != null)
                .Select(a => new { Day = a.Timestamp.AddHours(offsetHours).Date, Id = a.StaffId!.Value })
                .Distinct()
                .ToListAsync();

            var teacherDays = await granted
                .Where(a => a.TeacherId != null)
                .Select(a => new { Day = a.Timestamp.AddHours(offsetHours).Date, Id = a.TeacherId!.Value })
                .Distinct()
                .ToListAsync();

            var perDay = new Dictionary<DateTime, int>();
            void Add(DateTime day)
            {
                perDay[day] = perDay.TryGetValue(day, out var c) ? c + 1 : 1;
            }
            foreach (var r in studentDays) Add(r.Day);
            foreach (var r in staffDays) Add(r.Day);
            foreach (var r in teacherDays) Add(r.Day);

            // Hafta boshidan bugungacha o'tgan kunlar (1..7)
            var daysElapsed = (int)(todayLocal - weekStartLocal).TotalDays + 1;
            daysElapsed = Math.Clamp(daysElapsed, 1, 7);

            double sum = 0;
            for (int i = 0; i < daysElapsed; i++)
            {
                var day = weekStartLocal.AddDays(i);
                sum += perDay.TryGetValue(day, out var c) ? c : 0;
            }

            var attendance = sum / (daysElapsed * (double)totalActive) * 100.0;
            return Math.Round(Math.Clamp(attendance, 0, 100), 1);
        }
    }
}
