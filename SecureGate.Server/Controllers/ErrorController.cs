using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Models;

namespace SecureGate.Api.Controllers
{
    /// <summary>
    /// Production'da UseExceptionHandler("/error") shu yerga yo'naltiradi.
    /// Bu endpoint bo'lmasa har bir istisno 404/401 ko'rinishida qaytardi.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class ErrorController : ApiControllerBase
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger) => _logger = logger;

        [Route("/error")]
        public IActionResult Handle()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (feature?.Error is not null)
            {
                _logger.LogError(feature.Error, "Kutilmagan server xatosi: {Path}", feature.Path);
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("Kutilmagan server xatosi."));
        }
    }
}
