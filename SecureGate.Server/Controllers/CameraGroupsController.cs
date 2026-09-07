using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
using SecureGate.Data;
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
        private readonly AppDbContext _db;

        public CameraGroupsController(ICameraService cameraService, AppDbContext db)
        {
            _cameraService = cameraService;
            _db = db;
        }

        // ICameraService kamera guruhlariga scope qo'llamaydi (u Infrastructure hududida),
        // shuning uchun cheklovni shu yerda — controller darajasida qo'llaymiz.
        private Task<List<int>?> AllowedGroupIdsAsync() =>
            CameraScopeHelper.GetAllowedGroupIdsAsync(_db, User);

        [HttpGet]
        [SwaggerOperation(Summary = "Kamera guruhlari ro'yxati (kameralar bilan)")]
        public async Task<IActionResult> Index()
        {
            var allowed = await AllowedGroupIdsAsync();
            var list = await _cameraService.GetGroupsListAsync();

            if (allowed is not null)
                list = list.Where(g => allowed.Contains(g.Id)).ToList();

            return OkResponse(list);
        }

        [HttpGet("simple")]
        [SwaggerOperation(Summary = "Soddalashtirilgan ro'yxat (dropdownlar uchun)")]
        public async Task<IActionResult> Simple()
        {
            var allowed = await AllowedGroupIdsAsync();
            var list = await _cameraService.GetGroupsAsync();

            if (allowed is not null)
                list = list.Where(g => allowed.Contains(g.Id)).ToList();

            return OkResponse(CameraGroupResponseDto.FromMany(list));
        }

        [HttpGet("new")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Yangi guruh formasi (bo'sh model + mavjud kameralar)")]
        public async Task<IActionResult> NewForm()
        {
            var model = await _cameraService.BuildEmptyGroupFormAsync();
            await FilterAvailableCamerasAsync(model);
            return OkResponse(model);
        }

        [HttpGet("{id:int}")]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Tahrirlash uchun guruh")]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var allowed = await AllowedGroupIdsAsync();
            if (!CameraScopeHelper.IsGroupAllowed(allowed, id))
                return NotFoundResponse("Guruh topilmadi.");

            var model = await _cameraService.GetGroupForEditAsync(id);
            if (model == null) return NotFoundResponse("Guruh topilmadi.");

            await FilterAvailableCamerasAsync(model);
            return OkResponse(model);
        }

        [HttpPost]
        [HasPermission(Permission.CameraManage)]
        [SwaggerOperation(Summary = "Yangi guruh yaratish")]
        public async Task<IActionResult> Create([FromBody] CameraGroupFormViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var allowed = await AllowedGroupIdsAsync();
            if (!await CameraScopeHelper.AreCamerasAllowedAsync(_db, allowed, model.SelectedCameraIds))
                return FailResponse("Tanlangan kameralardan biri sizga ruxsat etilmagan.", StatusCodes.Status403Forbidden);

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

            var allowed = await AllowedGroupIdsAsync();
            if (!CameraScopeHelper.IsGroupAllowed(allowed, id))
                return NotFoundResponse("Guruh topilmadi.");

            // Begona kamerani o'z guruhiga ko'chirib olishning oldini olamiz.
            if (!await CameraScopeHelper.AreCamerasAllowedAsync(_db, allowed, model.SelectedCameraIds))
                return FailResponse("Tanlangan kameralardan biri sizga ruxsat etilmagan.", StatusCodes.Status403Forbidden);

            model.Id = id;

            try
            {
                var ok = await _cameraService.UpdateGroupAsync(model);
                if (!ok) return NotFoundResponse("Guruh topilmadi.");
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
            var allowed = await AllowedGroupIdsAsync();
            if (!CameraScopeHelper.IsGroupAllowed(allowed, id))
                return NotFoundResponse("Guruh topilmadi.");

            var ok = await _cameraService.DeleteGroupAsync(id);
            if (!ok) return NotFoundResponse("Guruh topilmadi.");
            return OkResponse("Guruh o'chirildi.");
        }

        /// <summary>Formadagi "mavjud kameralar" ro'yxatini ham scope bo'yicha cheklaymiz.</summary>
        private async Task FilterAvailableCamerasAsync(CameraGroupFormViewModel model)
        {
            var allowed = await AllowedGroupIdsAsync();
            var allowedCameraIds = await CameraScopeHelper.GetAllowedCameraIdsAsync(_db, allowed);
            if (allowedCameraIds is null) return;

            model.AvailableCameras = model.AvailableCameras
                .Where(c => allowedCameraIds.Contains(c.Id))
                .ToList();
        }
    }
}
