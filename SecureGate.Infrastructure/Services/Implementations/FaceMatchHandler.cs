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
    //
    // VAQT KONVENSIYASI: DB va tarmoqda hamma vaqt UTC.
    public class FaceMatchHandler : IFaceMatchHandler
    {
        // Statistika QIMMAT (bir nechta COUNT so'rovi) — ko'pi bilan 5 soniyada bir marta.
        private static readonly TimeSpan StatsWindow = TimeSpan.FromSeconds(5);

        // Bloklangan shaxs har ~3 soniyada qayta aniqlanadi — bir shaxs+turniket
        // juftligi uchun 30 soniyada bir marta ogohlantiramiz.
        private static readonly TimeSpan BlockedAlertWindow = TimeSpan.FromSeconds(30);

        // Notanish yuz eng ko'p spam manbai — bir kamera uchun 60 soniyada bir marta.
        private static readonly TimeSpan UnknownAlertWindow = TimeSpan.FromSeconds(60);

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly IHubContext<DashboardHub> _dashboardHub;
        private readonly IHubContext<AlertHub> _alertHub;
        private readonly ITurnstileService _turnstileService;
        private readonly IConfiguration _config;
        private readonly ILogger<FaceMatchHandler> _logger;

        public FaceMatchHandler(
            AppDbContext db,
            IWebHostEnvironment env,
            IHubContext<CameraHub> cameraHub,
            IHubContext<DashboardHub> dashboardHub,
            IHubContext<AlertHub> alertHub,
            ITurnstileService turnstileService,
            IConfiguration config,
            ILogger<FaceMatchHandler> logger)
        {
            _db = db;
            _env = env;
            _cameraHub = cameraHub;
            _dashboardHub = dashboardHub;
            _alertHub = alertHub;
            _turnstileService = turnstileService;
            _config = config;
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

            // Deterministik tartib — bir nechta turniket bo'lsa har doim bir xil ketma-ketlik.
            var turnstiles = camera.LinkedTurnstiles.OrderBy(t => t.Id).ToList();

            // Turniketga bog'lanmagan kamera "Granted" AccessLog yozmasligi kerak —
            // hech kim hech qayerdan o'tmagan. Bunday kamera Regular sifatida sozlanishi
            // va CameraSightingHandler orqali CameraUser yozuvi yaratishi kerak.
            if (turnstiles.Count == 0)
            {
                _logger.LogWarning(
                    "Kamera #{CameraId} Turnstile turida, lekin unga turniket bog'lanmagan — " +
                    "AccessLog yozilmadi. Kamerani Regular turiga o'tkazing yoki turniket bog'lang.",
                    ev.CameraId);
                return;
            }

            string? snapshotPath = null;
            if (ev.SnapshotJpeg != null && ev.SnapshotJpeg.Length > 0)
                snapshotPath = await SaveSnapshotAsync(ev.SnapshotJpeg, ct);

            // Shaxs statusi DB'dan QAYTA o'qiladi — KnownFaceCache eskirgan bo'lishi mumkin
            // (bloklangan odam cache yangilangunicha o'tib ketmasligi uchun).
            var (personActive, statusReason) = await CheckPersonStatusAsync(ev, ct);

            var logs = new List<(AccessLog Log, Turnstile Turnstile, bool Granted, string Reason)>();

            foreach (var turnstile in turnstiles)
            {
                bool granted;
                string reason;

                if (!personActive)
                {
                    granted = false;
                    reason = statusReason;
                }
                else
                {
                    (granted, reason) = await CheckTurnstilePermissionAsync(ev, turnstile, ct);
                }

                var log = new AccessLog
                {
                    CameraId = ev.CameraId,
                    TurnstileId = turnstile.Id,
                    Method = AccessMethod.Face,
                    Result = granted ? AccessResult.Granted : AccessResult.Denied,
                    FaceConfidence = ev.Confidence * 100,
                    CapturedImagePath = snapshotPath,
                    Details = reason,
                    // DB'da HAMMA VAQT UTC saqlanadi; mahalliy vaqtga o'girish — UI ishi.
                    Timestamp = DateTime.UtcNow
                };

                switch (ev.PersonType)
                {
                    case "Student": log.StudentId = ev.PersonId; break;
                    case "Staff": log.StaffId = ev.PersonId; break;
                    case "Teacher": log.TeacherId = ev.PersonId; break;
                }

                _db.AccessLogs.Add(log);
                logs.Add((log, turnstile, granted, reason));
            }

            await _db.SaveChangesAsync(ct);

            // Ruxsat berilgan HAR BIR turniket ochiladi (avval faqat birinchisi ochilardi).
            foreach (var entry in logs.Where(e => e.Granted))
            {
                try
                {
                    await _turnstileService.OpenAsync(entry.Turnstile.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Turniket #{Id} ochishda xato", entry.Turnstile.Id);
                }
            }

            // /AccessLog/Index sahifasi avtomatik yangilanishi uchun event
            foreach (var entry in logs)
            {
                try
                {
                    await _cameraHub.Clients.All.SendAsync("NewAccessLog", new
                    {
                        id = entry.Log.Id,
                        fullName = ev.FullName,
                        personType = ev.PersonType,
                        isUnknown = ev.PersonType == "Unknown",
                        method = entry.Log.Method.ToString(),
                        methodLabel = entry.Log.Method.GetDisplayName(),
                        result = entry.Log.Result.ToString(),
                        resultLabel = entry.Log.Result.GetDisplayName(),
                        turnstileName = entry.Turnstile.Name,
                        cameraName = camera.Name,
                        // ISO-8601 UTC — formatlash frontend ishi.
                        timestamp = entry.Log.Timestamp.ToString("O"),
                        confidence = entry.Log.FaceConfidence,
                        granted = entry.Granted,
                        reason = entry.Reason
                    }, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "NewAccessLog SignalR event yuborishda xato");
                }

                // Dashboard live-feed va ogohlantirishlar. Har biri o'z ichida
                // try/catch bilan o'ralgan — SignalR xatosi turniket ochilishini
                // yoki AccessLog yozilishini HECH QACHON to'xtatmaydi.
                await BroadcastActivityAsync(ev, camera, entry, ct);
                await BroadcastAlertsAsync(ev, camera, entry, ct);
            }

            // Statistika throttle bilan (5s) — pastdagi izohga qarang.
            await BroadcastStatsAsync(ct);
        }

        /// <summary>
        /// DashboardHub → "NewActivity". Dashboard'dagi "Real-time faoliyat" ro'yxati
        /// REST'dan kelgan <c>recentActivity</c> elementlari bilan bir xil shaklda
        /// bo'lishi uchun maydonlar <see cref="AccessLogItemViewModel"/> ga mos:
        /// userName / action / time / type.
        /// </summary>
        private async Task BroadcastActivityAsync(
            FaceMatchEvent ev,
            Camera camera,
            (AccessLog Log, Turnstile Turnstile, bool Granted, string Reason) entry,
            CancellationToken ct)
        {
            try
            {
                var userName = ev.PersonType == "Unknown" || string.IsNullOrWhiteSpace(ev.FullName)
                    ? "Noma'lum"
                    : ev.FullName;

                await _dashboardHub.Clients.All.SendAsync("NewActivity", new
                {
                    userName,
                    action = entry.Granted
                        ? $"{entry.Turnstile.Name} — kirdi"
                        : $"{entry.Turnstile.Name} — rad etildi ({entry.Reason})",
                    // DIQQAT: `type` — feed ikonkasining rangi uchun ("good"/"deny"/"warn"),
                    // REST DashboardViewModel.RecentActivity bilan AYNI shakl.
                    // Shaxs turi alohida `personType` maydonida yuboriladi.
                    type = entry.Granted
                        ? "good"
                        : ev.PersonType == "Unknown" ? "warn" : "deny",
                    personType = ev.PersonType,
                    cameraName = camera.Name,
                    turnstileName = entry.Turnstile.Name,
                    granted = entry.Granted,
                    // ISO-8601 UTC — formatlash frontend ishi.
                    time = entry.Log.Timestamp.ToString("O")
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NewActivity SignalR event yuborishda xato");
            }
        }

        /// <summary>
        /// AlertHub → "BlockedAccessAttempt" (tanish, lekin rad etilgan shaxs) yoki
        /// "NewAlert" (notanish yuz). Spam'ning oldini olish uchun throttle qo'yilgan.
        /// </summary>
        private async Task BroadcastAlertsAsync(
            FaceMatchEvent ev,
            Camera camera,
            (AccessLog Log, Turnstile Turnstile, bool Granted, string Reason) entry,
            CancellationToken ct)
        {
            if (entry.Granted) return;

            try
            {
                if (ev.PersonType == "Unknown" || !ev.PersonId.HasValue)
                {
                    // Notanish yuz har ~3 soniyada qayta aniqlanadi → kamera bo'yicha 60s.
                    if (!RealtimeThrottle.TryAcquire($"alert:unknown:{camera.Id}", UnknownAlertWindow))
                        return;

                    await _alertHub.Clients.All.SendAsync("NewAlert", new
                    {
                        title = "Noma'lum yuz aniqlandi",
                        message = $"{camera.Name} kamerasida ro'yxatdan o'tmagan shaxs " +
                                  $"\"{entry.Turnstile.Name}\" turniketidan o'tmoqchi bo'ldi.",
                        type = "warning", // info | warning | danger | success
                        time = DateTime.UtcNow.ToString("O")
                    }, ct);
                    return;
                }

                // Tanish shaxs rad etildi (bloklangan / ruxsat yo'q / turniket bloklangan).
                var key = $"alert:blocked:{ev.PersonType}:{ev.PersonId}:{entry.Turnstile.Id}";
                if (!RealtimeThrottle.TryAcquire(key, BlockedAlertWindow))
                    return;

                await _alertHub.Clients.All.SendAsync("BlockedAccessAttempt", new
                {
                    userName = string.IsNullOrWhiteSpace(ev.FullName) ? "Noma'lum" : ev.FullName,
                    turnstileName = entry.Turnstile.Name,
                    reason = entry.Reason,
                    time = DateTime.UtcNow.ToString("O")
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AlertHub SignalR event yuborishda xato");
            }
        }

        /// <summary>
        /// DashboardHub → "StatsUpdated".
        ///
        /// <para>
        /// TANLANGAN VARIANT: <b>(a) throttle</b>. Sabab: frontend stat kartochkalari
        /// to'liq (absolyut) qiymatlarni ko'rsatadi va REST <c>/dashboard</c> javobi
        /// bilan bir xil shaklda bo'lishi kerak. Delta yuborilsa klientda hisoblagichlar
        /// REST bilan tez orada rasinxron bo'lardi (bir nechta tab, uzilgan ulanish,
        /// boshqa manbalardan o'zgarishlar). Shu sababli bu yerda faqat 4 ta yengil
        /// COUNT so'rovi bajariladi (DashboardService dagi og'ir GroupBy / recent
        /// activity / popular turnstiles so'rovlari TAKRORLANMAYDI) va natija ko'pi
        /// bilan 5 soniyada bir marta yuboriladi.
        /// </para>
        /// </summary>
        private async Task BroadcastStatsAsync(CancellationToken ct)
        {
            if (!RealtimeThrottle.TryAcquire("dashboard:stats", StatsWindow))
                return;

            try
            {
                // "Bugun" — mahalliy kun; DB'da vaqt UTC. DashboardService bilan bir xil qoida.
                var offsetHours = _config.GetValue<double?>("App:LocalUtcOffsetHours") ?? 5;
                var offset = TimeSpan.FromHours(offsetHours);
                var fromUtc = (DateTime.UtcNow + offset).Date - offset;
                var toUtc = fromUtc.AddDays(1);

                var activeStudents = await _db.Students
                    .CountAsync(s => s.Status == StudentStatus.Active, ct);

                var todayPass = await _db.AccessLogs.CountAsync(a =>
                    a.Timestamp >= fromUtc && a.Timestamp < toUtc && a.Result == AccessResult.Granted, ct);

                var activeCameras = await _db.Cameras
                    .CountAsync(c => c.Status == CameraStatus.Online, ct);

                var alerts = await _db.Alerts.CountAsync(ct);

                await _dashboardHub.Clients.All.SendAsync("StatsUpdated", new
                {
                    activeStudents,
                    todayPass,
                    activeCameras,
                    alerts,
                    time = DateTime.UtcNow.ToString("O")
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StatsUpdated SignalR event yuborishda xato");
            }
        }

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

        /// <summary>
        /// Shaxsning DB'dagi HOZIRGI statusini tekshiradi. KnownFaceCache 60s gacha
        /// eskirgan bo'lishi mumkin — bloklangan odam shu oraliqda o'tib ketmasligi uchun
        /// status har bir voqeada DB'dan qayta o'qiladi.
        /// </summary>
        private async Task<(bool active, string reason)> CheckPersonStatusAsync(
            FaceMatchEvent ev, CancellationToken ct)
        {
            if (ev.PersonType == "Unknown" || !ev.PersonId.HasValue)
                return (false, "Noma'lum yuz — kirish rad etildi");

            var id = ev.PersonId.Value;

            switch (ev.PersonType)
            {
                case "Student":
                {
                    var status = await _db.Students
                        .Where(s => s.Id == id)
                        .Select(s => (StudentStatus?)s.Status)
                        .FirstOrDefaultAsync(ct);

                    if (status == null) return (false, "O'quvchi topilmadi");
                    if (status == StudentStatus.Blocked) return (false, "Foydalanuvchi bloklangan");
                    if (status == StudentStatus.Archived) return (false, "Foydalanuvchi arxivlangan");
                    return (true, "");
                }
                case "Staff":
                {
                    var status = await _db.StaffMembers
                        .Where(s => s.Id == id)
                        .Select(s => (StaffStatus?)s.Status)
                        .FirstOrDefaultAsync(ct);

                    if (status == null) return (false, "Xodim topilmadi");
                    if (status != StaffStatus.Active) return (false, "Xodim faol emas — kirish rad etildi");
                    return (true, "");
                }
                case "Teacher":
                {
                    var status = await _db.Teachers
                        .Where(t => t.Id == id)
                        .Select(t => (TeacherStatus?)t.Status)
                        .FirstOrDefaultAsync(ct);

                    if (status == null) return (false, "O'qituvchi topilmadi");
                    if (status != TeacherStatus.Active) return (false, "O'qituvchi faol emas — kirish rad etildi");
                    return (true, "");
                }
                default:
                    return (false, "Noma'lum shaxs turi — kirish rad etildi");
            }
        }

        private async Task<(bool granted, string reason)> CheckTurnstilePermissionAsync(
            FaceMatchEvent ev,
            Turnstile turnstile,
            CancellationToken ct)
        {
            if (turnstile.Status == TurnstileStatus.Blocked)
                return (false, "Turniket bloklangan");

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
