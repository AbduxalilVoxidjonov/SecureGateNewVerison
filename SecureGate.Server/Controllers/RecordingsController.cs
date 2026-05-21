using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Cameras;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/recordings")]
    [HasPermission(Permission.RecordingsView)]
    public class RecordingsController : ApiControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly IWebHostEnvironment _env;

        private const int ArchiveDays = 30;
        private const string RecordingsFolder = "recordings";

        public RecordingsController(ICameraService cameraService, IWebHostEnvironment env)
        {
            _cameraService = cameraService;
            _env = env;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Yozuvlar mavjud bo'lgan kameralar ro'yxati")]
        public async Task<IActionResult> Index()
        {
            var data = await _cameraService.GetCamerasAsync(null, null, null);
            return OkResponse(data.Cameras);
        }

        [HttpGet("camera/{id:int}")]
        [SwaggerOperation(Summary = "Kamera bo'yicha arxiv ro'yxati (oxirgi 30 kun)")]
        public async Task<IActionResult> Camera(int id)
        {
            var camera = await _cameraService.GetByIdAsync(id);
            if (camera == null) return FailResponse("Kamera topilmadi.", StatusCodes.Status404NotFound);

            var today = DateTime.Today;
            var earliest = camera.CreatedAt.Date > today.AddDays(-ArchiveDays + 1)
                ? camera.CreatedAt.Date
                : today.AddDays(-ArchiveDays + 1);

            var cameraFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", RecordingsFolder, $"cam-{camera.Id}");

            var entries = new List<RecordingArchiveEntry>();
            for (var date = today; date >= earliest; date = date.AddDays(-1))
            {
                var fileName = $"{date:yyyy-MM-dd}.mp4";
                var fullPath = Path.Combine(cameraFolder, fileName);
                var exists = System.IO.File.Exists(fullPath);
                long sizeBytes = 0;
                if (exists)
                {
                    try { sizeBytes = new FileInfo(fullPath).Length; } catch { }
                }

                entries.Add(new RecordingArchiveEntry
                {
                    Date = date,
                    FileName = fileName,
                    Exists = exists,
                    SizeBytes = sizeBytes
                });
            }

            return OkResponse(new RecordingArchiveViewModel
            {
                Camera = camera,
                Entries = entries
            });
        }

        [HttpGet("camera/{id:int}/download")]
        [SwaggerOperation(Summary = "Ma'lum sana uchun yozuvni yuklab olish")]
        public async Task<IActionResult> Download(int id, [FromQuery] string date)
        {
            var camera = await _cameraService.GetByIdAsync(id);
            if (camera == null) return FailResponse("Kamera topilmadi.", StatusCodes.Status404NotFound);

            if (!DateTime.TryParse(date, out var parsed))
                return FailResponse("Sana noto'g'ri.");

            var fileName = $"{parsed:yyyy-MM-dd}.mp4";
            var fullPath = Path.Combine(_env.WebRootPath ?? "wwwroot", RecordingsFolder, $"cam-{camera.Id}", fileName);

            if (!System.IO.File.Exists(fullPath))
                return FailResponse($"Yozuv topilmadi: {fileName}", StatusCodes.Status404NotFound);

            var safeCode = string.IsNullOrWhiteSpace(camera.CameraCode) ? $"cam-{camera.Id}" : camera.CameraCode;
            var downloadName = $"{safeCode}_{fileName}";
            var stream = System.IO.File.OpenRead(fullPath);
            return File(stream, "video/mp4", downloadName);
        }
    }
}
