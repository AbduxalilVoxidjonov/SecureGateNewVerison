using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Access;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace SecureGate.Api.Controllers
{
    [Route("api/turnstiles")]
    [HasPermission(Permission.TurnstileView)]
    public class TurnstilesController : ApiControllerBase
    {
        private readonly ITurnstileService _service;
        private readonly IDeviceConnectionTester _connectionTester;
        private readonly ILogger<TurnstilesController> _logger;

        public TurnstilesController(
            ITurnstileService service,
            IDeviceConnectionTester connectionTester,
            ILogger<TurnstilesController> logger)
        {
            _service = service;
            _connectionTester = connectionTester;
            _logger = logger;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Barcha turniketlar")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            // LinkedCamera navigatsiyasi orqali RTSP credential'lari chiqib ketmasin.
            CameraSecrets.ScrubAll(list.Select(t => t.LinkedCamera));
            return OkResponse(list);
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Turniket ma'lumoti")]
        public async Task<IActionResult> GetById(int id)
        {
            var turn = await _service.GetByIdAsync(id);
            if (turn == null) return NotFoundResponse("Turniket topilmadi.");
            CameraSecrets.Scrub(turn.LinkedCamera);
            CameraSecrets.ScrubAll(turn.AccessLogs.Select(a => a.Camera));
            return OkResponse(turn);
        }

        [HttpPost]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Yangi turniket qo'shish")]
        public async Task<IActionResult> Create([FromBody] TurnstileCreateViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            try
            {
                var created = await _service.CreateAsync(model);
                CameraSecrets.Scrub(created.LinkedCamera);
                return OkResponse(created, "Turniket qo'shildi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpPost("test-connection")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniket ulanishini tekshirish (saqlamasdan)")]
        public async Task<IActionResult> TestConnection([FromBody] TurnstileTestConnectionViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var result = await _connectionTester.TestTcpAsync(model.IpAddress, model.Port, ct);
            return OkResponse(result, result.Message);
        }

        [HttpPost("{id:int}/open")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni ochish")]
        public async Task<IActionResult> Open(int id)
        {
            var ok = await _service.OpenAsync(id);
            if (!ok) return NotFoundResponse("Turniket topilmadi.");
            return OkResponse("Turniket ochildi.");
        }

        [HttpPost("{id:int}/close")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni yopish")]
        public async Task<IActionResult> Close(int id)
        {
            var ok = await _service.CloseAsync(id);
            if (!ok) return NotFoundResponse("Turniket topilmadi.");
            return OkResponse("Turniket yopildi.");
        }

        [HttpPost("{id:int}/block")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni bloklash")]
        public async Task<IActionResult> Block(int id)
        {
            var ok = await _service.BlockAsync(id);
            if (!ok) return NotFoundResponse("Turniket topilmadi.");
            return OkResponse("Turniket bloklandi.");
        }

        [HttpPost("{id:int}/unblock")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni blokdan chiqarish")]
        public async Task<IActionResult> Unblock(int id)
        {
            var ok = await _service.UnblockAsync(id);
            if (!ok) return NotFoundResponse("Turniket topilmadi.");
            return OkResponse("Turniket blokdan chiqarildi.");
        }

        [HttpPost("emergency-open")]
        [SuperAdminOnly]
        [SwaggerOperation(
            Summary = "Favqulodda holatda — barcha turniketlarni ochish",
            Description = "Faqat SuperAdmin. Majburiy `reason` talab qilinadi va chaqiruv audit uchun loglanadi.")]
        public async Task<IActionResult> EmergencyOpen([FromBody] EmergencyOpenRequest request)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var reason = request.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
                return FailResponse("Favqulodda ochish uchun sabab ko'rsatilishi shart.");

            var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "noma'lum";
            var actorName = User.Identity?.Name ?? "noma'lum";

            _logger.LogWarning(
                "FAVQULODDA OCHISH: barcha turniketlar ochilmoqda. UserId={UserId}, User={UserName}, Vaqt={TimeUtc}, Sabab={Reason}, IP={Ip}",
                actor, actorName, DateTime.UtcNow, reason, HttpContext.Connection.RemoteIpAddress?.ToString());

            await _service.EmergencyOpenAllAsync();

            _logger.LogWarning("FAVQULODDA OCHISH bajarildi. UserId={UserId}, Sabab={Reason}", actor, reason);

            return OkResponse("Barcha turniketlar favqulodda ochildi.");
        }
    }
}
