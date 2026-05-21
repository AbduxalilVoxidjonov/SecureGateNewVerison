using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // Oddiy (Regular) kamera yuzni aniqlasa shu handler chaqiriladi.
    // Hech qanday turniket ochmaydi va AccessLog'ga yozmaydi —
    // faqat CameraUser jadvaliga "qachon, qaysi kamerada, kim ko'rindi" yozadi.
    // Saqlangandan keyin SignalR orqali "NewSighting" event yuboradi —
    // /CameraUser/Index sahifasi avtomatik yangilanadi.
    public class CameraSightingHandler : ICameraSightingHandler
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly ILogger<CameraSightingHandler> _logger;

        public CameraSightingHandler(
            AppDbContext db,
            IWebHostEnvironment env,
            IHubContext<CameraHub> cameraHub,
            ILogger<CameraSightingHandler> logger)
        {
            _db = db;
            _env = env;
            _cameraHub = cameraHub;
            _logger = logger;
        }

        public async Task HandleAsync(FaceMatchEvent ev, CancellationToken ct = default)
        {
            string? snapshotPath = null;
            if (ev.SnapshotJpeg != null && ev.SnapshotJpeg.Length > 0)
                snapshotPath = await SaveSnapshotAsync(ev.SnapshotJpeg, ct);

            // Ism-familyani PersonType bo'yicha aniqlaymiz
            var (firstName, lastName) = SplitName(ev.FullName);

            var sighting = new CameraUser
            {
                FirstName = string.IsNullOrWhiteSpace(firstName) ? "Noma'lum" : firstName,
                LastName = lastName,
                UserType = MapUserType(ev.PersonType),
                CameraId = ev.CameraId,
                Confidence = ev.Confidence * 100,
                CapturedImagePath = snapshotPath,
                DetectedAt = DateTime.Now,
                IsReviewed = false
            };

            switch (ev.PersonType)
            {
                case "Student": sighting.StudentId = ev.PersonId; break;
                case "Staff": sighting.StaffId = ev.PersonId; break;
                case "Teacher": sighting.TeacherId = ev.PersonId; break;
            }

            _db.CameraUsers.Add(sighting);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CameraUser yozishda xato (camera #{Id})", ev.CameraId);
                return;
            }

            // Saqlandi → /CameraUser/Index sahifasi avtomatik yangilanishi uchun event
            var camera = await _db.Cameras
                .Where(c => c.Id == ev.CameraId)
                .Select(c => new { c.Name })
                .FirstOrDefaultAsync(ct);

            try
            {
                await _cameraHub.Clients.All.SendAsync("NewSighting", new
                {
                    id = sighting.Id,
                    fullName = sighting.FullName,
                    initials = sighting.Initials,
                    userType = sighting.UserType.ToString(),
                    userTypeLabel = sighting.UserType.GetDisplayName(),
                    cameraId = ev.CameraId,
                    cameraName = camera?.Name ?? "—",
                    detectedAt = sighting.DetectedAt.ToString("dd.MM.yyyy HH:mm"),
                    confidence = sighting.Confidence,
                    imagePath = sighting.CapturedImagePath,
                    isUnknown = ev.PersonType == "Unknown"
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewSighting SignalR event yuborishda xato");
            }
        }

        private static (string firstName, string lastName) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
            var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => ("", ""),
                1 => (parts[0], ""),
                _ => (parts[0], parts[1])
            };
        }

        private static CameraUserType MapUserType(string personType) => personType switch
        {
            "Student" => CameraUserType.Student,
            "Teacher" => CameraUserType.Teacher,
            "Staff" => CameraUserType.Staff,
            _ => CameraUserType.Unknown
        };

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
    }
}
