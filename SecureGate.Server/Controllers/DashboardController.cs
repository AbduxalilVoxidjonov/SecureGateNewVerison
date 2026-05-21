using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ApiControllerBase
    {
        private readonly IDashboardService _service;

        public DashboardController(IDashboardService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Asosiy dashboard ma'lumotlari")]
        public async Task<IActionResult> Index()
        {
            var data = await _service.GetDashboardDataAsync();
            return OkResponse(data);
        }
    }
}
