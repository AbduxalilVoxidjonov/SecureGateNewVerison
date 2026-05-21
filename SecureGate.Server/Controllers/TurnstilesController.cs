using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Access;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/turnstiles")]
    [HasPermission(Permission.TurnstileView)]
    public class TurnstilesController : ApiControllerBase
    {
        private readonly ITurnstileService _service;
        private readonly IDeviceConnectionTester _connectionTester;

        public TurnstilesController(ITurnstileService service, IDeviceConnectionTester connectionTester)
        {
            _service = service;
            _connectionTester = connectionTester;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Barcha turniketlar")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return OkResponse(list);
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Turniket ma'lumoti")]
        public async Task<IActionResult> GetById(int id)
        {
            var turn = await _service.GetByIdAsync(id);
            if (turn == null) return FailResponse("Turniket topilmadi.", StatusCodes.Status404NotFound);
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
            if (!ok) return FailResponse("Turniket topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Turniket ochildi.");
        }

        [HttpPost("{id:int}/close")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni yopish")]
        public async Task<IActionResult> Close(int id)
        {
            var ok = await _service.CloseAsync(id);
            if (!ok) return FailResponse("Turniket topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Turniket yopildi.");
        }

        [HttpPost("{id:int}/block")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni bloklash")]
        public async Task<IActionResult> Block(int id)
        {
            var ok = await _service.BlockAsync(id);
            if (!ok) return FailResponse("Turniket topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Turniket bloklandi.");
        }

        [HttpPost("{id:int}/unblock")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Turniketni blokdan chiqarish")]
        public async Task<IActionResult> Unblock(int id)
        {
            var ok = await _service.UnblockAsync(id);
            if (!ok) return FailResponse("Turniket topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Turniket blokdan chiqarildi.");
        }

        [HttpPost("emergency-open")]
        [HasPermission(Permission.TurnstileManage)]
        [SwaggerOperation(Summary = "Favqulodda holatda — barcha turniketlarni ochish")]
        public async Task<IActionResult> EmergencyOpen()
        {
            await _service.EmergencyOpenAllAsync();
            return OkResponse("Barcha turniketlar favqulodda ochildi.");
        }
    }
}
