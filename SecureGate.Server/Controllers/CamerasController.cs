using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain;
using SecureGate.Domain.Auth;
using SecureGate.Domain.Cameras;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Cameras;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/cameras")]
    [HasPermission(Permission.CameraView)]
    public class CamerasController : ApiControllerBase
    {
        private readonly ICameraService _cameraService;
        private readonly IDeviceConnectionTester _connectionTester;
        private readonly ICameraMjpegStreamer _streamer;

        public CamerasController(
            ICameraService cameraService,
            IDeviceConnectionTester connectionTester,
            ICameraMjpegStreamer streamer)
        {
            _cameraService = cameraService;
            _connectionTester = connectionTester;
            _streamer = streamer;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Kameralar ro'yxati (filter)")]
        public async Task<IActionResult> Index(
            [FromQuery] int? groupId,
            [FromQuery] CameraStatus? status,
            [FromQuery] string? search)
        {
            var data = await _cameraService.GetCamerasAsync(groupId, status, search);
            return OkResponse(new
            {
                cameras = data.Cameras,
                groups = data.CameraGroups,
                filters = new
                {
                    groupId = data.SelectedGroupId,
                    status = data.StatusFilter,
                    search = data.SearchTerm
                }
            });
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Kamera ma'lumoti")]
        public async Task<IActionResult> GetById(int id)
        {
            var cam = await _cameraService.GetByIdAsync(id);
            if (cam == null) return FailResponse("Kamera topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse(cam);
        }

        [HttpPost]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Yangi kamera qo'shish")]
        public async Task<IActionResult> Create([FromBody] CameraCreateViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            try
            {
                var created = await _cameraService.CreateAsync(model);
                return OkResponse(created, "Kamera qo'shildi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpGet("{id:int}/stream")]
        [SwaggerOperation(Summary = "Kamera jonli video oqimi (MJPEG)")]
        public async Task Stream(int id, [FromQuery] int? w, CancellationToken ct)
        {
            var cam = await _cameraService.GetByIdAsync(id);
            if (cam == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            Response.Headers.Pragma = "no-cache";

            try
            {
                await _streamer.StreamAsync(cam, Response.Body, w, ct);
            }
            catch (OperationCanceledException)
            {
                // Mijoz oynani yopdi — normal holat
            }
        }

        [HttpGet("{id:int}/snapshot")]
        [SwaggerOperation(Summary = "Kamera bitta kadr (JPEG)")]
        public async Task<IActionResult> Snapshot(int id, [FromQuery] int? w, CancellationToken ct)
        {
            var cam = await _cameraService.GetByIdAsync(id);
            if (cam == null) return NotFound();

            byte[]? jpeg;
            try
            {
                jpeg = await _streamer.SnapshotAsync(cam, w, ct);
            }
            catch (OperationCanceledException)
            {
                return new EmptyResult();
            }

            if (jpeg == null) return StatusCode(StatusCodes.Status503ServiceUnavailable);

            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            return File(jpeg, "image/jpeg");
        }

        [HttpPost("test-connection")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Kamera ulanishini tekshirish (saqlamasdan)")]
        public async Task<IActionResult> TestConnection([FromBody] CameraTestConnectionViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var result = await _connectionTester.TestCameraAsync(model, ct);
            return OkResponse(result, result.Message);
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Kamerani tahrirlash")]
        public async Task<IActionResult> Update(int id, [FromBody] CameraEditViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            model.Id = id;

            try
            {
                var ok = await _cameraService.UpdateAsync(model);
                if (!ok) return FailResponse("Kamera topilmadi.", StatusCodes.Status404NotFound);
                return OkResponse("Kamera yangilandi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Kamerani o'chirish")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _cameraService.DeleteAsync(id);
            if (!ok) return FailResponse("Kamera topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Kamera o'chirildi.");
        }
    }
}
