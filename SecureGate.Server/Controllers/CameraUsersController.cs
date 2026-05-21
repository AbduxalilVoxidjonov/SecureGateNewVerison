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
    [Route("api/camera-users")]
    [HasPermission(Permission.CameraUserView)]
    public class CameraUsersController : ApiControllerBase
    {
        private readonly ICameraUserService _service;

        public CameraUsersController(ICameraUserService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Kameralarda aniqlangan foydalanuvchilar (filter)")]
        public async Task<IActionResult> Index(
            [FromQuery] string? search,
            [FromQuery] int? cameraId,
            [FromQuery] CameraUserType? userType,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] bool? reviewedOnly,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var model = await _service.GetListAsync(search, cameraId, userType, dateFrom, dateTo, reviewedOnly, page, pageSize);
            return OkResponse(new
            {
                items = model.Items,
                cameras = model.Cameras,
                page = model.CurrentPage,
                pageSize = model.PageSize,
                totalCount = model.TotalCount,
                totalPages = model.TotalPages,
                todayCount = model.TodayCount,
                unknownCount = model.UnknownCount,
                uniquePeopleCount = model.UniquePeopleCount,
                filters = new
                {
                    search = model.SearchTerm,
                    cameraId = model.CameraId,
                    userType = model.UserType,
                    dateFrom = model.DateFrom,
                    dateTo = model.DateTo,
                    reviewedOnly = model.ReviewedOnly
                }
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return FailResponse("Yozuv topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse(item);
        }

        [HttpPost]
        [HasPermission(Permission.CameraUserManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Yangi camera-user yozuvi qo'shish")]
        public async Task<IActionResult> Create([FromForm] CameraUserCreateViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            var created = await _service.CreateAsync(model);
            return OkResponse(created, "Yozuv qo'shildi.");
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permission.CameraUserManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Camera-user yozuvini tahrirlash")]
        public async Task<IActionResult> Update(int id, [FromForm] CameraUserEditViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            model.Id = id;
            var ok = await _service.UpdateAsync(model);
            if (!ok) return FailResponse("Yozuv topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Yozuv yangilandi.");
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permission.CameraUserManage)]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return OkResponse("Yozuv o'chirildi.");
        }

        [HttpPost("{id:int}/reviewed")]
        [HasPermission(Permission.CameraUserManage)]
        [SwaggerOperation(Summary = "Yozuvni \"ko'rib chiqilgan\" deb belgilash")]
        public async Task<IActionResult> MarkReviewed(int id, [FromQuery] bool reviewed = true)
        {
            var ok = await _service.MarkReviewedAsync(id, reviewed);
            if (!ok) return FailResponse("Yozuv topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse();
        }

        [HttpGet("stats")]
        [SwaggerOperation(Summary = "Statistika")]
        public async Task<IActionResult> Stats([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var stats = await _service.GetStatsAsync(from, to);
            return OkResponse(stats);
        }
    }
}
