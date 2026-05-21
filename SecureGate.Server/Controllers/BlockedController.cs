using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/blocked")]
    [HasPermission(Permission.BlockedManage)]
    public class BlockedController : ApiControllerBase
    {
        private readonly IUsersService _usersService;

        public BlockedController(IUsersService usersService)
        {
            _usersService = usersService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Bloklangan o'quvchilar ro'yxati")]
        public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            var model = await _usersService.GetStudentsAsync(null, null, StudentStatus.Blocked, page, pageSize);
            return OkResponse(new
            {
                items = model.Students,
                page = model.CurrentPage,
                pageSize = model.PageSize,
                totalCount = model.TotalCount,
                totalPages = model.TotalPages
            });
        }
    }
}
