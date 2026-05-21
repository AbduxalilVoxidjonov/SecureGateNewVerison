using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // Aniqlangan yuz uchun **og'ir** pipeline: snapshot saqlash, AccessLog yozish,
    // turniketni ochish. CameraHub.FaceDetected event yashil quti uchun
    // CameraStreamWorker'dan yuboriladi (har aniqlanishda).
    // Bu handler esa cooldown bilan chaqiriladi va AccessLog'ga yozgandan keyin
    // "NewAccessLog" event yuboradi — /AccessLog/Index sahifasi avtomatik yangilanadi.
    // Scoped — har bir voqea uchun yangi scope ochiladi.
    public class FaceMatchHandler : IFaceMatchHandler
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly ITurnstileService _turnstileService;
        private readonly ILogger<FaceMatchHandler> _logger;

        public FaceMatchHandler(
            AppDbContext db,
            IWebHostEnvironment env,
            IHubContext<CameraHub> cameraHub,
            ITurnstileService turnstileService,
            ILogger<FaceMatchHandler> logger)
        {
            _db = db;
            _env = env;
            _cameraHub = cameraHub;
            _turnstileService = turnstileService;
            _logger = logger;
        }

        public async Task HandleAsync(FaceMatchEvent ev, CancellationToken ct = default)
        {
            var camera = await _db.Cameras
                .Include(c => c.LinkedTurnstiles)
                .FirstOrDefaultAsync(c => c.Id == ev.CameraId, ct);

            if (camera == null)
            {
                _logger.LogWarning("Noma'lum kamera: {CameraId}", ev.CameraId);
                return;
            }

            string? snapshotPath = null;
            if (ev.SnapshotJpeg != null && ev.SnapshotJpeg.Length > 0)
                snapshotPath = await SaveSnapshotAsync(ev.SnapshotJpeg, ct);

            var turnstile = camera.LinkedTurnstiles.FirstOrDefault();
            var (granted, reason) = await CheckPermissionAsync(ev, turnstile, ct);

            var log = new AccessLog
            {
                CameraId = ev.CameraId,
                TurnstileId = turnstile?.Id,
                Method = AccessMethod.Face,
                Result = granted ? AccessResult.Granted : AccessResult.Denied,
                FaceConfidence = ev.Confidence * 100,
                CapturedImagePath = snapshotPath,
                Details = reason,
                // Mahalliy vaqt (Asia/Tashkent) — UI'da ham shu vaqt ko'rsatiladi.
                // Loyiha bitta vaqt zonasida ishlashga mo'ljallangani uchun UTC saqlash shart emas.
                Timestamp = DateTime.Now
            };

            switch (ev.PersonType)
            {
                case "Student": log.StudentId = ev.PersonId; break;
                case "Staff": log.StaffId = ev.PersonId; break;
                case "Teacher": log.TeacherId = ev.PersonId; break;
            }

            _db.AccessLogs.Add(log);
            await _db.SaveChangesAsync(ct);

            if (granted && turnstile != null)
                await _turnstileService.OpenAsync(turnstile.Id);

            // /AccessLog/Index sahifasi avtomatik yangilanishi uchun event
            try
            {
                await _cameraHub.Clients.All.SendAsync("NewAccessLog", new
                {
                    id = log.Id,
                    fullName = ev.FullName,
                    personType = ev.PersonType,
                    isUnknown = ev.PersonType == "Unknown",
                    method = log.Method.ToString(),
                    methodLabel = log.Method.GetDisplayName(),
                    result = log.Result.ToString(),
                    resultLabel = log.Result.GetDisplayName(),
                    turnstileName = turnstile?.Name ?? "—",
                    cameraName = camera.Name,
                    timestamp = log.Timestamp.ToString("dd.MM HH:mm:ss"),
                    confidence = log.FaceConfidence,
                    granted,
                    reason
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewAccessLog SignalR event yuborishda xato");
            }
        }

        private async Task<string?> SaveSnapshotAsync(byte[] jpegBytes, CancellationToken ct)
        {
            try
            {
                var dir = Path.Combine(_env.WebRootPath, "uploads", "snapshots");
                Directory.CreateDirectory(dir);

                var fileName = $"{Guid.NewGuid():N}.jpg";
                var fullPath = Path.Combine(dir, fileName);
                await File.WriteAllBytesAsync(fullPath, jpegBytes, ct);

                return $"/uploads/snapshots/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Snapshot saqlashda xato");
                return null;
            }
        }

        private async Task<(bool granted, string reason)> CheckPermissionAsync(
            FaceMatchEvent ev,
            Turnstile? turnstile,
            CancellationToken ct)
        {
            if (ev.PersonType == "Unknown" || !ev.PersonId.HasValue)
                return (false, "Noma'lum yuz — kirish rad etildi");

            if (turnstile == null)
                return (true, "Kamera turniketga bog'lanmagan — log only");

            var permission = await _db.TurnstilePermissions.FirstOrDefaultAsync(p =>
                p.TurnstileId == turnstile.Id &&
                ((ev.PersonType == "Student" && p.StudentId == ev.PersonId) ||
                 (ev.PersonType == "Staff" && p.StaffId == ev.PersonId) ||
                 (ev.PersonType == "Teacher" && p.TeacherId == ev.PersonId)), ct);

            if (permission == null)
                return (false, "Bu turniketga ruxsat tayinlanmagan");

            if (!permission.IsAllowed)
                return (false, "Foydalanuvchi bloklangan");

            return (true, "Muvaffaqiyatli");
        }
    }
}
