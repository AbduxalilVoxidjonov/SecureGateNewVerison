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

        protected IActionResult ValidationFail()
        {
            var errors = ModelState
                .Where(kvp => kvp.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return BadRequest(ApiResponse.Fail("Validatsiya xatosi", errors));
        }
    }
}
