using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Cameras;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/camera-groups")]
    [HasPermission(Permission.CameraView)]
    public class CameraGroupsController : ApiControllerBase
    {
        private readonly ICameraService _cameraService;

        public CameraGroupsController(ICameraService cameraService)
        {
            _cameraService = cameraService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Kamera guruhlari ro'yxati (kameralar bilan)")]
        public async Task<IActionResult> Index()
        {
            var list = await _cameraService.GetGroupsListAsync();
            return OkResponse(list);
        }

        [HttpGet("simple")]
        [SwaggerOperation(Summary = "Soddalashtirilgan ro'yxat (dropdownlar uchun)")]
        public async Task<IActionResult> Simple()
        {
            var list = await _cameraService.GetGroupsAsync();
            return OkResponse(list);
        }

        [HttpGet("new")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Yangi guruh formasi (bo'sh model + mavjud kameralar)")]
        public async Task<IActionResult> NewForm()
        {
            var model = await _cameraService.BuildEmptyGroupFormAsync();
            return OkResponse(model);
        }

        [HttpGet("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Tahrirlash uchun guruh")]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var model = await _cameraService.GetGroupForEditAsync(id);
            if (model == null) return FailResponse("Guruh topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse(model);
        }

        [HttpPost]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Yangi guruh yaratish")]
        public async Task<IActionResult> Create([FromBody] CameraGroupFormViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            try
            {
                var id = await _cameraService.CreateGroupAsync(model);
                return OkResponse(new { id }, "Guruh yaratildi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Guruhni yangilash")]
        public async Task<IActionResult> Update(int id, [FromBody] CameraGroupFormViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            model.Id = id;

            try
            {
                var ok = await _cameraService.UpdateGroupAsync(model);
                if (!ok) return FailResponse("Guruh topilmadi.", StatusCodes.Status404NotFound);
                return OkResponse("Guruh yangilandi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Guruhni o'chirish")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _cameraService.DeleteGroupAsync(id);
            if (!ok) return FailResponse("Guruh topilmadi.", StatusCodes.Status404NotFound);
            return OkResponse("Guruh o'chirildi.");
        }
    }
}
