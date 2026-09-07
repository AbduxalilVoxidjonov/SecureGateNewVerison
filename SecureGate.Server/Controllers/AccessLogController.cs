using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
using SecureGate.Domain.Access;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/access-logs")]
    [HasPermission(Permission.AccessLogsView)]
    public class AccessLogController : ApiControllerBase
    {
        private readonly IAccessLogService _service;

        public AccessLogController(IAccessLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Kirish jurnali (filter + pagination)")]
        public async Task<IActionResult> Index(
            [FromQuery] string? search,
            [FromQuery] AccessResult? result,
            [FromQuery] AccessMethod? method,
            [FromQuery] int? turnstileId,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15)
        {
            var (safePage, safeSize) = Paging(page, pageSize);
            var model = await _service.GetLogsAsync(search, result, method, turnstileId, dateFrom, dateTo, safePage, safeSize);

            // AccessLog.Camera / Turnstile.LinkedCamera orqali RTSP credential'lari chiqib ketmasin.
            CameraSecrets.ScrubAll(model.Logs.Select(l => l.Camera));
            CameraSecrets.ScrubAll(model.Turnstiles.Select(t => t.LinkedCamera));

            return OkResponse(new
            {
                items = model.Logs,
                turnstiles = model.Turnstiles,
                page = model.CurrentPage,
                pageSize = model.PageSize,
                totalCount = model.TotalCount,
                totalPages = model.TotalPages,
                filters = new
                {
                    search = model.SearchTerm,
                    result = model.ResultFilter,
                    method = model.MethodFilter,
                    turnstileId = model.TurnstileId,
                    dateFrom = model.DateFrom,
                    dateTo = model.DateTo
                }
            });
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Bitta yozuv")]
        public async Task<IActionResult> GetById(int id)
        {
            var log = await _service.GetByIdAsync(id);
            if (log == null) return NotFoundResponse("Yozuv topilmadi.");

            CameraSecrets.Scrub(log.Camera);
            CameraSecrets.Scrub(log.Turnstile?.LinkedCamera);

            return OkResponse(log);
        }
    }
}
