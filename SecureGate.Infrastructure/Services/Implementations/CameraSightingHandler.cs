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
        // Notanish yuz har ~3 soniyada qayta aniqlanadi — bir kamera uchun
        // 60 soniyada bir marta ogohlantiramiz (FaceMatchHandler bilan bir xil qoida).
        private static readonly TimeSpan UnknownAlertWindow = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly IHubContext<DashboardHub> _dashboardHub;
        private readonly IHubContext<AlertHub> _alertHub;
        private readonly ILogger<CameraSightingHandler> _logger;

        public CameraSightingHandler(
            AppDbContext db,
            IWebHostEnvironment env,
            IHubContext<CameraHub> cameraHub,
            IHubContext<DashboardHub> dashboardHub,
            IHubContext<AlertHub> alertHub,
            ILogger<CameraSightingHandler> logger)
        {
            _db = db;
            _env = env;
            _cameraHub = cameraHub;
            _dashboardHub = dashboardHub;
            _alertHub = alertHub;
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
                // DB'da HAMMA VAQT UTC saqlanadi; mahalliy vaqtga o'girish — UI ishi.
                DetectedAt = DateTime.UtcNow,
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
                    // ISO-8601 UTC — formatlash frontend ishi.
                    detectedAt = sighting.DetectedAt.ToString("O"),
                    confidence = sighting.Confidence,
                    imagePath = sighting.CapturedImagePath,
                    isUnknown = ev.PersonType == "Unknown"
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewSighting SignalR event yuborishda xato");
            }

            var cameraName = camera?.Name ?? "—";

            // Dashboard "Real-time faoliyat" ro'yxati: oddiy kamera ko'rishi ham faollik.
            // type = "info" — turniketdan o'tish emas, shunchaki ko'rindi.
            try
            {
                await _dashboardHub.Clients.All.SendAsync("NewActivity", new
                {
                    userName = sighting.FullName,
                    action = $"{cameraName} kamerasida ko'rindi",
                    type = "info", // good | deny | warn | info — feed ikonkasi uchun
                    personType = ev.PersonType,
                    cameraName,
                    granted = (bool?)null,
                    // ISO-8601 UTC — formatlash frontend ishi.
                    time = sighting.DetectedAt.ToString("O")
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewActivity SignalR event yuborishda xato");
            }

            // Notanish yuz — ogohlantirish (kamera bo'yicha throttle bilan).
            if (ev.PersonType == "Unknown" &&
                RealtimeThrottle.TryAcquire($"alert:unknown:{ev.CameraId}", UnknownAlertWindow))
            {
                try
                {
                    await _alertHub.Clients.All.SendAsync("NewAlert", new
                    {
                        title = "Noma'lum yuz aniqlandi",
                        message = $"{cameraName} kamerasida ro'yxatdan o'tmagan shaxs aniqlandi.",
                        type = "warning", // info | warning | danger | success
                        time = DateTime.UtcNow.ToString("O")
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NewAlert SignalR event yuborishda xato");
                }
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

                // Disk cheksiz to'lmasligi uchun snapshot kichraytiriladi (maks 640px, JPEG q75).
                var bytes = SnapshotImage.Downscale(jpegBytes);

                var fileName = $"{Guid.NewGuid():N}.jpg";
                var fullPath = Path.Combine(dir, fileName);
                await File.WriteAllBytesAsync(fullPath, bytes, ct);

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
