using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
using SecureGate.Domain.Auth;
using SecureGate.Domain.Cameras;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    /// <summary>
    /// Yozuvlar arxivi — NVR (registrator) qattiq diskidan o'qiladi.
    /// Ilgari bu controller wwwroot/recordings/cam-{id}/*.mp4 fayllarini qidirardi,
    /// lekin o'sha fayllarni hech kim yozmasdi — arxiv doim bo'sh edi va foydalanuvchiga
    /// yolg'on ma'lumot ko'rsatilardi. Endi haqiqiy manba — NVR.
    /// </summary>
    [Route("api/recordings")]
    [HasPermission(Permission.RecordingsView)]
    public class RecordingsController : ApiControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly INvrArchiveService _nvrArchive;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecordingsController> _logger;

        // Bir kunlik yozuv bir necha GB bo'ladi — brauzerni o'ldirmaslik uchun cheklaymiz.
        private const int DefaultMaxDownloadHours = 4;
        private const int DefaultMaxSearchDays = 7;

        private const string NotNvrMessage =
            "Bu kamera NVR kanali sifatida sozlanmagan — arxiv mavjud emas.";

        public RecordingsController(
            ICameraService cameraService,
            INvrArchiveService nvrArchive,
            IConfiguration configuration,
            ILogger<RecordingsController> logger)
        {
            _cameraService = cameraService;
            _nvrArchive = nvrArchive;
            _configuration = configuration;
            _logger = logger;
        }

        private int MaxDownloadHours => PositiveOrDefault(
            _configuration.GetValue<int?>("Nvr:MaxDownloadHours"), DefaultMaxDownloadHours);

        private int MaxSearchDays => PositiveOrDefault(
            _configuration.GetValue<int?>("Nvr:MaxSearchDays"), DefaultMaxSearchDays);

        // ─────────────────────────── Kameralar ro'yxati ───────────────────────────

        [HttpGet]
        [SwaggerOperation(Summary = "Arxiv uchun kameralar ro'yxati")]
        public async Task<IActionResult> Index()
        {
            // GetCamerasAsync admin kamera-guruh scope'ini o'zi qo'llaydi
            // (CamerasController.Index bilan bir xil naqsh).
            var data = await _cameraService.GetCamerasAsync(null, null, null);

            var cameras = data.Cameras
                .Select(c => RecordingCameraDto.From(c, SupportsSafe(c)))
                .ToList();

            return OkResponse(cameras);
        }

        // ─────────────────────────── Arxiv qidiruvi ───────────────────────────

        [HttpGet("camera/{id:int}")]
        [SwaggerOperation(Summary = "Kamera arxivi (NVR) — berilgan oraliqdagi yozuv bo'laklari")]
        public async Task<IActionResult> Camera(
            int id,
            [FromQuery] string? from,
            [FromQuery] string? to,
            CancellationToken ct)
        {
            // GetByIdAsync admin kamera-guruh filtrini qo'llaydi — begona kamera 404 qaytaradi.
            var camera = await _cameraService.GetByIdAsync(id);
            if (camera == null) return NotFoundResponse("Kamera topilmadi.");

            if (!TryResolveRange(from, to, defaultWindow: TimeSpan.FromHours(24),
                    out var fromUtc, out var toUtc, out var rangeError))
                return FailResponse(rangeError!);

            var maxDays = MaxSearchDays;
            if ((toUtc - fromUtc).TotalDays > maxDays)
                return FailResponse($"Qidiruv oralig'i {maxDays} kundan oshmasligi kerak.");

            var response = new CameraArchiveResponseDto
            {
                Camera = RecordingCameraBriefDto.From(camera),
                ArchiveSupported = false,
                From = fromUtc,
                To = toUtc,
                Segments = new List<ArchiveSegmentDto>()
            };

            if (!SupportsSafe(camera))
            {
                response.Message = NotNvrMessage;
                return OkResponse(response, NotNvrMessage);
            }

            response.ArchiveSupported = true;

            try
            {
                var segments = await _nvrArchive.SearchAsync(camera, fromUtc, toUtc, ct);
                response.Segments = ArchiveSegmentDto.FromMany(segments);
                return OkResponse(response);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Klient sahifani yopdi — bu xato emas.
                _logger.LogDebug("Arxiv qidiruvi klient tomonidan bekor qilindi (kamera {CameraId}).", id);
                return new EmptyResult();
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "NVR arxiv qidiruvi vaqt chegarasidan oshdi (kamera {CameraId}).", id);
                return FailResponse("NVR belgilangan vaqtda javob bermadi.", StatusCodes.Status504GatewayTimeout);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "NVR arxiv qidiruvi muvaffaqiyatsiz (kamera {CameraId}).", id);
                return FailResponse($"NVR bilan bog'lanib bo'lmadi: {ex.Message}", StatusCodes.Status502BadGateway);
            }
        }

        // ─────────────────────────── Yuklab olish ───────────────────────────

        [HttpGet("camera/{id:int}/download")]
        [SwaggerOperation(Summary = "Kamera arxividan oraliqni mp4 sifatida yuklab olish")]
        public async Task<IActionResult> Download(
            int id,
            [FromQuery] string? from,
            [FromQuery] string? to,
            CancellationToken ct)
        {
            // Token query-string orqali kelishi mumkin (?access_token=) — Program.cs
            // JwtBearerEvents.OnMessageReceived "/download" bilan tugaydigan yo'llarni qo'llab-quvvatlaydi.
            var camera = await _cameraService.GetByIdAsync(id);
            if (camera == null) return NotFoundResponse("Kamera topilmadi.");

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return FailResponse("Yuklab olish uchun 'from' va 'to' parametrlari majburiy.");

            if (!TryResolveRange(from, to, defaultWindow: null,
                    out var fromUtc, out var toUtc, out var rangeError))
                return FailResponse(rangeError!);

            var maxHours = MaxDownloadHours;
            if ((toUtc - fromUtc).TotalHours > maxHours)
                return FailResponse(
                    $"Yuklab olish oralig'i {maxHours} soatdan oshmasligi kerak. " +
                    "Uzoq oraliqni bo'laklarga bo'lib yuklang.");

            if (!SupportsSafe(camera))
                return FailResponse(NotNvrMessage);

            var fileName = BuildFileName(camera, fromUtc);

            Stream? stream = null;
            try
            {
                stream = await _nvrArchive.OpenPlaybackAsync(camera, fromUtc, toUtc, ct);

                // enableRangeProcessing: false — fragmentli mp4 oqimi seek qilinmaydi,
                // Range so'rovi kelsa oqim buziladi. FileStreamResult oqimni o'zi dispose qiladi,
                // dispose esa ichkaridagi ffmpeg jarayonini to'xtatadi.
                return File(stream, "video/mp4", fileName, enableRangeProcessing: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await DisposeQuietlyAsync(stream);
                _logger.LogDebug("Arxiv yuklab olish klient tomonidan bekor qilindi (kamera {CameraId}).", id);
                return new EmptyResult();
            }
            catch (OperationCanceledException ex)
            {
                await DisposeQuietlyAsync(stream);
                _logger.LogWarning(ex, "NVR playback vaqt chegarasidan oshdi (kamera {CameraId}).", id);
                return FailResponse("NVR belgilangan vaqtda javob bermadi.", StatusCodes.Status504GatewayTimeout);
            }
            catch (InvalidOperationException ex)
            {
                await DisposeQuietlyAsync(stream);
                _logger.LogWarning(ex, "NVR playback ochilmadi (kamera {CameraId}).", id);
                return FailResponse($"NVR bilan bog'lanib bo'lmadi: {ex.Message}", StatusCodes.Status502BadGateway);
            }
        }

        // ─────────────────────────── Yordamchilar ───────────────────────────

        /// <summary>
        /// Supports() servis ichida kutilmagan xato bersa ham ro'yxat butunlay yiqilmasin.
        /// </summary>
        private bool SupportsSafe(Camera camera)
        {
            try
            {
                return _nvrArchive.Supports(camera);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NVR Supports() tekshiruvi xato berdi (kamera {CameraId}).", camera.Id);
                return false;
            }
        }

        /// <summary>
        /// from/to ni UTC'ga keltiradi va validatsiya qiladi.
        /// defaultWindow berilgan bo'lsa — bo'sh parametrlar shu oyna bilan to'ldiriladi.
        /// </summary>
        private static bool TryResolveRange(
            string? from,
            string? to,
            TimeSpan? defaultWindow,
            out DateTime fromUtc,
            out DateTime toUtc,
            out string? error)
        {
            fromUtc = default;
            toUtc = default;
            error = null;

            var now = DateTime.UtcNow;
            var hasFrom = !string.IsNullOrWhiteSpace(from);
            var hasTo = !string.IsNullOrWhiteSpace(to);

            if (hasFrom && !TryParseUtc(from, out fromUtc))
            {
                error = "'from' sanasi noto'g'ri. ISO-8601 UTC formatini yuboring (masalan 2026-09-07T00:00:00Z).";
                return false;
            }

            if (hasTo && !TryParseUtc(to, out toUtc))
            {
                error = "'to' sanasi noto'g'ri. ISO-8601 UTC formatini yuboring (masalan 2026-09-07T23:59:59Z).";
                return false;
            }

            if (!hasFrom || !hasTo)
            {
                if (defaultWindow is null)
                {
                    error = "'from' va 'to' parametrlari majburiy.";
                    return false;
                }

                // Biri berilgan bo'lsa — ikkinchisini standart oyna bilan to'ldiramiz.
                if (!hasTo) toUtc = now;
                if (!hasFrom) fromUtc = toUtc - defaultWindow.Value;
            }

            if (toUtc <= fromUtc)
            {
                error = "Oraliq noto'g'ri: 'to' qiymati 'from' dan katta bo'lishi kerak.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// ISO-8601 sanani UTC'ga o'giradi. Offset ko'rsatilmagan qiymat UTC deb qabul qilinadi
        /// (server mahalliy vaqt zonasiga bog'lanib qolmaslik uchun).
        /// </summary>
        private static bool TryParseUtc(string? value, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            if (!DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    // DIQQAT: RoundtripKind ni AdjustToUniversal bilan BIRGA ishlatib bo'lmaydi —
                    // .NET ArgumentException tashlaydi. AssumeUniversal + AdjustToUniversal:
                    // offset ko'rsatilmagan qiymat UTC deb olinadi, offsetli qiymat UTC'ga o'giriladi.
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed))
                return false;

            utc = parsed.Kind == DateTimeKind.Utc
                ? parsed
                : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        private static string BuildFileName(Camera camera, DateTime fromUtc)
        {
            var code = string.IsNullOrWhiteSpace(camera.CameraCode)
                ? $"cam-{camera.Id}"
                : camera.CameraCode;

            return $"{Sanitize(code)}-{fromUtc:yyyyMMdd-HHmmss}.mp4";
        }

        /// <summary>Content-Disposition sarlavhasini buzadigan belgilarni olib tashlaydi.</summary>
        private static string Sanitize(string value)
        {
            var chars = value
                .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
                .ToArray();

            var result = new string(chars).Trim('-');
            return string.IsNullOrEmpty(result) ? "camera" : result;
        }

        private static int PositiveOrDefault(int? value, int fallback)
            => value is > 0 ? value.Value : fallback;

        private async Task DisposeQuietlyAsync(Stream? stream)
        {
            if (stream is null) return;
            try
            {
                await stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "NVR playback oqimini yopishda xato.");
            }
        }
    }
}
