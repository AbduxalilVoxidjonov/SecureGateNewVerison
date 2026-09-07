using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Models;

namespace SecureGate.Api.Controllers
{
    [ApiController]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult OkResponse<T>(T data, string? message = null)
            => Ok(ApiResponse<T>.Ok(data, message));

        protected IActionResult OkResponse(string? message = null)
            => Ok(ApiResponse.Ok(message));

        protected IActionResult FailResponse(string message, int statusCode = StatusCodes.Status400BadRequest)
            => StatusCode(statusCode, ApiResponse.Fail(message));

        /// <summary>404 + ApiResponse shakli (takrorlanuvchi shablonni bir joyga yig'adi).</summary>
        protected IActionResult NotFoundResponse(string message)
            => StatusCode(StatusCodes.Status404NotFound, ApiResponse.Fail(message));

        protected IActionResult ValidationFail()
        {
            var errors = ModelState
                .Where(kvp => kvp.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return BadRequest(ApiResponse.Fail("Validatsiya xatosi", errors));
        }

        /// <summary>
        /// Sahifalash parametrlarini xavfsiz chegaraga keltiradi.
        /// (pageSize=10000000 -> OOM, page=0 -> Skip(-N) xatosi oldini oladi.)
        /// </summary>
        protected static (int Page, int Size) Paging(int page, int size, int max = 100)
            => (page < 1 ? 1 : page, size < 1 ? 10 : (size > max ? max : size));
    }
}
