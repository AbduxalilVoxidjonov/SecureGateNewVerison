using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.People;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/staff")]
    [HasPermission(Permission.StaffView)]
    public class StaffController : ApiControllerBase
    {
        private readonly IStaffService _staffService;

        public StaffController(IStaffService staffService)
        {
            _staffService = staffService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Xodimlar ro'yxati")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _staffService.GetAllAsync();
            return OkResponse(list);
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "Xodim ma'lumoti")]
        public async Task<IActionResult> GetById(int id)
        {
            var staff = await _staffService.GetByIdAsync(id);
            if (staff == null) return NotFoundResponse("Xodim topilmadi.");
            return OkResponse(staff);
        }

        [HttpPost]
        [HasPermission(Permission.StaffManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Yangi xodim qo'shish")]
        public async Task<IActionResult> Create([FromForm] StaffCreateViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            try
            {
                var created = await _staffService.CreateAsync(model);
                return OkResponse(created, "Xodim qo'shildi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permission.StaffManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Xodimni tahrirlash")]
        public async Task<IActionResult> Update(int id, [FromForm] StaffEditViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            model.Id = id;

            try
            {
                var ok = await _staffService.UpdateAsync(model);
                if (!ok) return NotFoundResponse("Xodim topilmadi.");
                return OkResponse("Xodim yangilandi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permission.StaffManage)]
        [SwaggerOperation(Summary = "Xodimni o'chirish")]
        public async Task<IActionResult> Delete(int id)
        {
            // Servis Task qaytaradi — yo'q yozuv uchun 200 qaytmasligi kerak.
            if (await _staffService.GetByIdAsync(id) is null)
                return NotFoundResponse("Xodim topilmadi.");

            await _staffService.DeleteAsync(id);
            return OkResponse("Xodim o'chirildi.");
        }
    }
}
