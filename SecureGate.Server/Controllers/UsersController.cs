using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
using SecureGate.Domain;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.People;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/users")]
    [HasPermission(Permission.UsersView)]
    public class UsersController : ApiControllerBase
    {
        private readonly IUsersService _usersService;
        private readonly ITurnstileService _turnstileService;

        public UsersController(IUsersService usersService, ITurnstileService turnstileService)
        {
            _usersService = usersService;
            _turnstileService = turnstileService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "O'quvchilar ro'yxati (filter + pagination)")]
        public async Task<IActionResult> Index(
            [FromQuery] string? search,
            [FromQuery] StudentStatus? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (safePage, safeSize) = Paging(page, pageSize);
            var model = await _usersService.GetStudentsAsync(search, status, safePage, safeSize);
            return OkResponse(new
            {
                items = model.Students,
                page = model.CurrentPage,
                pageSize = model.PageSize,
                totalCount = model.TotalCount,
                totalPages = model.TotalPages,
                search = model.SearchTerm,
                groupId = model.GroupId,
                status = model.StatusFilter
            });
        }

        [HttpGet("{id:int}")]
        [SwaggerOperation(Summary = "O'quvchi ma'lumoti")]
        public async Task<IActionResult> GetById(int id)
        {
            var student = await _usersService.GetByIdAsync(id);
            if (student == null) return NotFoundResponse("O'quvchi topilmadi.");
            return OkResponse(student);
        }

        [HttpPost]
        [HasPermission(Permission.UsersManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Yangi o'quvchi qo'shish (rasm bilan)")]
        public async Task<IActionResult> Create([FromForm] UsersCreateViewModel model)
        {
            if (model.PhotoFile == null && string.IsNullOrEmpty(model.CapturedPhotoBase64))
                ModelState.AddModelError(nameof(model.PhotoFile), "Yuz rasmi yuklanishi shart.");

            if (!ModelState.IsValid) return ValidationFail();

            try
            {
                var created = await _usersService.CreateAsync(model);
                return OkResponse(created, "O'quvchi qo'shildi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        [HasPermission(Permission.UsersManage)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "O'quvchini tahrirlash")]
        public async Task<IActionResult> Update(int id, [FromForm] UsersEditViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            model.Id = id;

            try
            {
                await _usersService.UpdateAsync(id, model);
                return OkResponse("O'quvchi yangilandi.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        [HasPermission(Permission.UsersDelete)]
        [SwaggerOperation(Summary = "O'quvchini o'chirish")]
        public async Task<IActionResult> Delete(int id)
        {
            // Servis Task qaytaradi (mavjudlik haqida xabar bermaydi) — shuning uchun
            // o'chirishdan oldin o'zimiz tekshiramiz, aks holda yo'q yozuv uchun ham 200 qaytardi.
            if (await _usersService.GetByIdAsync(id) is null)
                return NotFoundResponse("O'quvchi topilmadi.");

            await _usersService.DeleteAsync(id);
            return OkResponse("O'quvchi o'chirildi.");
        }

        [HttpPost("{id:int}/block")]
        [HasPermission(Permission.UsersManage)]
        [SwaggerOperation(Summary = "O'quvchini bloklash")]
        public async Task<IActionResult> Block(int id, [FromBody] BlockUserViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            if (await _usersService.GetByIdAsync(id) is null)
                return NotFoundResponse("O'quvchi topilmadi.");

            model.StudentId = id;
            await _usersService.BlockAsync(id, model);
            return OkResponse("O'quvchi bloklandi.");
        }

        [HttpPost("{id:int}/unblock")]
        [HasPermission(Permission.UsersManage)]
        [SwaggerOperation(Summary = "O'quvchini blokdan chiqarish")]
        public async Task<IActionResult> Unblock(int id)
        {
            if (await _usersService.GetByIdAsync(id) is null)
                return NotFoundResponse("O'quvchi topilmadi.");

            await _usersService.UnblockAsync(id);
            return OkResponse("O'quvchi blokdan chiqarildi.");
        }

        [HttpGet("turnstiles")]
        [SwaggerOperation(Summary = "Forma uchun mavjud turniketlar ro'yxati")]
        public async Task<IActionResult> AvailableTurnstiles()
        {
            var list = await _turnstileService.GetAllAsync();
            // Turnstile.LinkedCamera orqali RTSP credential'lari chiqib ketmasin.
            CameraSecrets.ScrubAll(list.Select(t => t.LinkedCamera));
            return OkResponse(list);
        }
    }
}
