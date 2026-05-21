using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/reports")]
    [HasPermission(Permission.ReportsView)]
    public class ReportsController : ApiControllerBase
    {
        private readonly IReportService _service;

        public ReportsController(IReportService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Haftalik hisobot ma'lumotlari")]
        public async Task<IActionResult> GetReport()
        {
            var data = await _service.GetReportDataAsync();
            return OkResponse(data);
        }
    }
}
