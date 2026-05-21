using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class CameraUserService : ICameraUserService
    {
        private readonly AppDbContext _db;
        private readonly IPhotoStorageService _photoStorage;

        public CameraUserService(AppDbContext db, IPhotoStorageService photoStorage)
        {
            _db = db;
            _photoStorage = photoStorage;
        }

        public async Task<CameraUserIndexViewModel> GetListAsync(
            string? search,
            int? cameraId,
            CameraUserType? userType,
            DateTime? dateFrom,
            DateTime? dateTo,
            bool? reviewedOnly,
            int page,
            int pageSize)
        {
            var query = _db.CameraUsers
                .Include(c => c.Camera)
                .Include(c => c.Student)
                .Include(c => c.Teacher)
                .Include(c => c.Staff)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c =>
                    c.FirstName.Contains(s) ||
                    c.LastName.Contains(s) ||
                    (c.Note != null && c.Note.Contains(s)));
            }

            if (cameraId.HasValue)
                query = query.Where(c => c.CameraId == cameraId);

            if (userType.HasValue)
                query = query.Where(c => c.UserType == userType);

            if (dateFrom.HasValue)
                query = query.Where(c => c.DetectedAt >= dateFrom.Value.Date);

            if (dateTo.HasValue)
            {
                var endOfDay = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(c => c.DetectedAt <= endOfDay);
            }

            if (reviewedOnly.HasValue)
                query = query.Where(c => c.IsReviewed == reviewedOnly.Value);

            var total = await query.CountAsync();

            var todayStart = DateTime.UtcNow.Date;
            var todayCount = await query.CountAsync(c => c.DetectedAt >= todayStart);
            var unknownCount = await query.CountAsync(c => c.UserType == CameraUserType.Unknown);
            var uniquePeople = await query
                .Select(c => new { c.FirstName, c.LastName })
                .Distinct()
                .CountAsync();

            var items = await query
                .OrderByDescending(c => c.DetectedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var cameras = await _db.Cameras.OrderBy(c => c.Name).ToListAsync();

            return new CameraUserIndexViewModel
            {
                Items = items,
                SearchTerm = search,
                CameraId = cameraId,
                UserType = userType,
                DateFrom = dateFrom,
                DateTo = dateTo,
                ReviewedOnly = reviewedOnly,
                Cameras = cameras,
                TotalCount = total,
                TodayCount = todayCount,
                UnknownCount = unknownCount,
                UniquePeopleCount = uniquePeople,
                CurrentPage = page,
                PageSize = pageSize
            };
        }

        public async Task<CameraUser?> GetByIdAsync(int id) =>
            await _db.CameraUsers
                .Include(c => c.Camera)
                .Include(c => c.Student)
                .Include(c => c.Teacher)
                .Include(c => c.Staff)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task<CameraUser> CreateAsync(CameraUserCreateViewModel model)
        {
            string? photoPath = null;
            if (model.Photo != null && model.Photo.Length > 0)
            {
                photoPath = await _photoStorage.SavePhotoAsync(model.Photo, null, "camera-users");
            }

            var entity = new CameraUser
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                UserType = model.UserType,
                CameraId = model.CameraId,
                DetectedAt = model.DetectedAt == default ? DateTime.UtcNow : model.DetectedAt.ToUniversalTime(),
                Confidence = model.Confidence,
                CapturedImagePath = photoPath,
                Note = model.Note
            };

            _db.CameraUsers.Add(entity);
            await _db.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> UpdateAsync(CameraUserEditViewModel model)
        {
            var entity = await _db.CameraUsers.FindAsync(model.Id);
            if (entity == null) return false;

            entity.FirstName = model.FirstName.Trim();
            entity.LastName = model.LastName.Trim();
            entity.UserType = model.UserType;
            entity.CameraId = model.CameraId;
            entity.DetectedAt = model.DetectedAt == default ? entity.DetectedAt : model.DetectedAt.ToUniversalTime();
            entity.Confidence = model.Confidence;
            entity.Note = model.Note;
            entity.IsReviewed = model.IsReviewed;

            if (model.Photo != null && model.Photo.Length > 0)
            {
                var newPath = await _photoStorage.SavePhotoAsync(model.Photo, null, "camera-users");
                if (!string.IsNullOrEmpty(newPath))
                {
                    _photoStorage.DeletePhoto(entity.CapturedImagePath);
                    entity.CapturedImagePath = newPath;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task DeleteAsync(int id)
        {
            var entity = await _db.CameraUsers.FindAsync(id);
            if (entity == null) return;

            var photoPath = entity.CapturedImagePath;
            _db.CameraUsers.Remove(entity);
            await _db.SaveChangesAsync();

            _photoStorage.DeletePhoto(photoPath);
        }

        public async Task<bool> MarkReviewedAsync(int id, bool reviewed)
        {
            var entity = await _db.CameraUsers.FindAsync(id);
            if (entity == null) return false;

            entity.IsReviewed = reviewed;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<CameraUserStatsViewModel> GetStatsAsync(DateTime? from, DateTime? to)
        {
            var query = _db.CameraUsers.AsQueryable();

            if (from.HasValue)
                query = query.Where(c => c.DetectedAt >= from.Value.Date);
            if (to.HasValue)
            {
                var endOfDay = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(c => c.DetectedAt <= endOfDay);
            }

            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = todayStart.AddDays(-(int)now.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var totalAllTime = await _db.CameraUsers.CountAsync();
            var totalToday = await _db.CameraUsers.CountAsync(c => c.DetectedAt >= todayStart);
            var totalWeek = await _db.CameraUsers.CountAsync(c => c.DetectedAt >= weekStart);
            var totalMonth = await _db.CameraUsers.CountAsync(c => c.DetectedAt >= monthStart);
            var unknownCount = await query.CountAsync(c => c.UserType == CameraUserType.Unknown);
            var reviewedCount = await query.CountAsync(c => c.IsReviewed);

            var byCamera = await query
                .Where(c => c.CameraId != null)
                .GroupBy(c => new { c.CameraId, c.Camera!.Name, c.Camera!.CameraCode })
                .Select(g => new CameraDetectionStat
                {
                    CameraId = g.Key.CameraId!.Value,
                    CameraName = g.Key.Name,
                    CameraCode = g.Key.CameraCode,
                    Count = g.Count()
                })
                .OrderByDescending(s => s.Count)
                .ToListAsync();

            var byUserType = await query
                .GroupBy(c => c.UserType)
                .Select(g => new UserTypeStat
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var byHour = await query
                .GroupBy(c => c.DetectedAt.Hour)
                .Select(g => new HourStat
                {
                    Hour = g.Key,
                    Count = g.Count()
                })
                .OrderBy(s => s.Hour)
                .ToListAsync();

            var topPeople = await query
                .GroupBy(c => new { c.FirstName, c.LastName })
                .Select(g => new TopPersonStat
                {
                    FullName = (g.Key.FirstName + " " + g.Key.LastName).Trim(),
                    DetectionCount = g.Count(),
                    LastSeen = g.Max(c => c.DetectedAt)
                })
                .OrderByDescending(s => s.DetectionCount)
                .Take(10)
                .ToListAsync();

            return new CameraUserStatsViewModel
            {
                TotalAllTime = totalAllTime,
                TotalToday = totalToday,
                TotalThisWeek = totalWeek,
                TotalThisMonth = totalMonth,
                UnknownCount = unknownCount,
                ReviewedCount = reviewedCount,
                ByCamera = byCamera,
                ByUserType = byUserType,
                ByHour = byHour,
                TopPeople = topPeople,
                DateFrom = from,
                DateTo = to
            };
        }
    }
}
