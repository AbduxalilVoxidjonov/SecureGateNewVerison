using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureGate.Api.Filters;
using SecureGate.Data;
using SecureGate.Domain.Auth;
using SecureGate.Domain.Cameras;
using SecureGate.Domain.People;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Admin;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/admins")]
    [SuperAdminOnly]
    public class AdminsController : ApiControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IPermissionService _permissionService;
        private readonly AppDbContext _db;

        public AdminsController(
            UserManager<AppUser> userManager,
            IPermissionService permissionService,
            AppDbContext db)
        {
            _userManager = userManager;
            _permissionService = permissionService;
            _db = db;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Adminlar ro'yxati")]
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.AsNoTracking().ToListAsync();
            var items = new List<AdminListItemViewModel>();

            foreach (var u in users)
            {
                var isSuper = await _userManager.IsInRoleAsync(u, Roles.SuperAdmin);
                var permCount = isSuper
                    ? Enum.GetValues<Permission>().Length
                    : await _db.UserPermissions.CountAsync(p => p.UserId == u.Id);

                items.Add(new AdminListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    IsActive = u.IsActive,
                    IsSuperAdmin = isSuper,
                    PermissionCount = permCount,
                    CreatedAt = u.CreatedAt
                });
            }

            return OkResponse(items.OrderByDescending(x => x.IsSuperAdmin).ThenBy(x => x.FullName).ToList());
        }

        [HttpGet("available-staff")]
        [SwaggerOperation(Summary = "Hali admin akkaunti yo'q xodimlar")]
        public async Task<IActionResult> AvailableStaff()
        {
            var staff = await GetAvailableStaffAsync();
            return OkResponse(staff);
        }

        [HttpGet("camera-groups")]
        [SwaggerOperation(Summary = "Tanlash uchun kamera guruhlari")]
        public async Task<IActionResult> CameraGroups()
        {
            var groups = await GetCameraGroupsAsync();
            return OkResponse(groups);
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Yangi admin yaratish (xodim asosida)")]
        public async Task<IActionResult> Create([FromBody] AdminCreateViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var staff = await _db.StaffMembers.FindAsync(model.StaffId);
            if (staff == null) return FailResponse("Tanlangan xodim topilmadi.");

            var alreadyLinked = await _db.Users.AnyAsync(u => u.StaffId == staff.Id);
            if (alreadyLinked) return FailResponse("Bu xodim uchun admin akkaunt allaqachon mavjud.");

            var generatedEmail = $"staff-{staff.Id}@securegate.local";

            var user = new AppUser
            {
                UserName = generatedEmail,
                Email = generatedEmail,
                FullName = staff.FullName,
                StaffId = staff.Id,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                var errs = result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(Models.ApiResponse.Fail("Yaratib bo'lmadi.", errs));
            }

            await _userManager.AddToRoleAsync(user, Roles.Admin);

            if (model.SelectedPermissions.Any())
                await _permissionService.SetPermissionsAsync(user.Id, model.SelectedPermissions);

            await SetCameraGroupsAsync(user.Id, model.SelectedCameraGroupIds);

            return OkResponse(new { id = user.Id, email = generatedEmail }, "Admin yaratildi.");
        }

        [HttpGet("{id}")]
        [SwaggerOperation(Summary = "Tahrirlash uchun admin ma'lumotlari")]
        public async Task<IActionResult> GetForEdit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return FailResponse("Admin topilmadi.", StatusCodes.Status404NotFound);

            var isSuper = await _userManager.IsInRoleAsync(user, Roles.SuperAdmin);
            var perms = await _permissionService.GetPermissionsAsync(id);
            var assignedGroupIds = await _db.UserCameraGroups
                .Where(x => x.UserId == id)
                .Select(x => x.CameraGroupId)
                .ToListAsync();

            var vm = new AdminEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                IsSuperAdmin = isSuper,
                SelectedPermissions = isSuper ? new List<Permission>() : perms.ToList(),
                SelectedCameraGroupIds = isSuper ? new List<int>() : assignedGroupIds,
                AvailableCameraGroups = await GetCameraGroupsAsync()
            };
            return OkResponse(vm);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(Summary = "Adminni tahrirlash")]
        public async Task<IActionResult> Update(string id, [FromBody] AdminEditViewModel model)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return FailResponse("Admin topilmadi.", StatusCodes.Status404NotFound);
            if (!ModelState.IsValid) return ValidationFail();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errs = updateResult.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(Models.ApiResponse.Fail("Yangilab bo'lmadi.", errs));
            }

            if (!await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
            {
                await _permissionService.SetPermissionsAsync(user.Id, model.SelectedPermissions);
                await SetCameraGroupsAsync(user.Id, model.SelectedCameraGroupIds);
            }

            return OkResponse("Admin yangilandi.");
        }

        [HttpPost("{id}/reset-password")]
        [SwaggerOperation(Summary = "Admin parolini qayta o'rnatish")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return FailResponse("Admin topilmadi.", StatusCodes.Status404NotFound);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!result.Succeeded)
            {
                var errs = result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(Models.ApiResponse.Fail("Parol o'zgartirilmadi.", errs));
            }

            return OkResponse("Parol o'zgartirildi.");
        }

        [HttpPost("{id}/toggle-active")]
        [SwaggerOperation(Summary = "Admin akkauntini faollashtirish/bloklash")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return FailResponse("Admin topilmadi.", StatusCodes.Status404NotFound);

            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                return FailResponse("SuperAdmin akkauntini bloklash mumkin emas.");

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            return OkResponse(new { isActive = user.IsActive });
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(Summary = "Adminni o'chirish")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return FailResponse("Admin topilmadi.", StatusCodes.Status404NotFound);

            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                return FailResponse("SuperAdmin akkauntini o'chirish mumkin emas.");

            await _userManager.DeleteAsync(user);
            return OkResponse("Admin o'chirildi.");
        }

        [HttpGet("permissions")]
        [SwaggerOperation(Summary = "Mavjud permissionlar ro'yxati (guruhlangan)")]
        public IActionResult Permissions()
        {
            var groups = PermissionGroups.All.Select(kv => new
            {
                groupName = kv.Key,
                permissions = kv.Value.Select(p => new
                {
                    code = p.ToString(),
                    value = (int)p,
                    label = PermissionGroups.LabelFor(p)
                })
            });
            return OkResponse(groups);
        }

        // ===== Helpers =====
        private async Task<List<Staff>> GetAvailableStaffAsync()
        {
            var linkedIds = await _db.Users
                .Where(u => u.StaffId != null)
                .Select(u => u.StaffId!.Value)
                .ToListAsync();

            return await _db.StaffMembers
                .Where(s => !linkedIds.Contains(s.Id))
                .OrderBy(s => s.FullName)
                .ToListAsync();
        }

        private async Task<List<CameraGroup>> GetCameraGroupsAsync() =>
            await _db.CameraGroups
                .Include(g => g.Cameras)
                .OrderBy(g => g.Name)
                .AsNoTracking()
                .ToListAsync();

        private async Task SetCameraGroupsAsync(string userId, IEnumerable<int> groupIds)
        {
            var existing = await _db.UserCameraGroups
                .Where(x => x.UserId == userId)
                .ToListAsync();
            _db.UserCameraGroups.RemoveRange(existing);

            foreach (var groupId in groupIds.Distinct())
            {
                _db.UserCameraGroups.Add(new UserCameraGroup
                {
                    UserId = userId,
                    CameraGroupId = groupId
                });
            }
            await _db.SaveChangesAsync();
        }
    }
}
