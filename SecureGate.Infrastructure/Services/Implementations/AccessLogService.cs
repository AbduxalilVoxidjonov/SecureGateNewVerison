using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class AccessLogService : IAccessLogService
    {
        private readonly AppDbContext _db;
        public AccessLogService(AppDbContext db) => _db = db;

        public async Task<AccessLogIndexViewModel> GetLogsAsync(string? search, AccessResult? result, AccessMethod? method, int? turnstileId, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
        {
            var query = _db.AccessLogs
                .Include(a => a.Student).Include(a => a.Teacher).Include(a => a.Staff)
                .Include(a => a.Turnstile).Include(a => a.Camera)
                .AsQueryable();

            if (result.HasValue) query = query.Where(a => a.Result == result);
            if (method.HasValue) query = query.Where(a => a.Method == method);
            if (turnstileId.HasValue) query = query.Where(a => a.TurnstileId == turnstileId);
            if (dateFrom.HasValue) query = query.Where(a => a.Timestamp >= dateFrom);
            if (dateTo.HasValue) query = query.Where(a => a.Timestamp <= dateTo);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(a =>
                    (a.Student != null && (a.Student.FirstName.Contains(search) || a.Student.LastName.Contains(search))) ||
                    (a.Teacher != null && a.Teacher.FullName.Contains(search)) ||
                    (a.Staff != null && a.Staff.FullName.Contains(search)));

            var total = await query.CountAsync();

            return new AccessLogIndexViewModel
            {
                Logs = await query.OrderByDescending(a => a.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
                SearchTerm = search,
                ResultFilter = result,
                MethodFilter = method,
                TurnstileId = turnstileId,
                Turnstiles = await _db.Turnstiles.ToListAsync(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = total
            };
        }

        public async Task<AccessLog?> GetByIdAsync(int id) =>
            await _db.AccessLogs
                .Include(a => a.Student).Include(a => a.Teacher).Include(a => a.Staff)
                .Include(a => a.Turnstile).Include(a => a.Camera)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task<AccessLog> LogAccessAsync(AccessLog log)
        {
            _db.AccessLogs.Add(log);
            await _db.SaveChangesAsync();
            return log;
        }
    }
}
