using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureGate.Api.Filters;
using SecureGate.Api.Models;
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
        private readonly ILogger<AdminsController> _logger;

        public AdminsController(
            UserManager<AppUser> userManager,
            IPermissionService permissionService,
            AppDbContext db,
            ILogger<AdminsController> logger)
        {
            _userManager = userManager;
            _permissionService = permissionService;
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Adminlar ro'yxati")]
        public async Task<IActionResult> Index()
        {
            var users = await _db.Users.AsNoTracking().ToListAsync();

            // N+1 o'rniga: rollar bir marta, permission sanoqlari bir marta (2 ta so'rov).
            var superIds = (await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin))
                .Select(u => u.Id)
                .ToHashSet();

            var permCounts = await _db.UserPermissions
                .GroupBy(p => p.UserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var totalPermissions = Enum.GetValues<Permission>().Length;

            var items = users.Select(u =>
            {
                var isSuper = superIds.Contains(u.Id);
                return new AdminListItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    IsActive = u.IsActive,
                    IsSuperAdmin = isSuper,
                    PermissionCount = isSuper
                        ? totalPermissions
                        : (permCounts.TryGetValue(u.Id, out var c) ? c : 0),
                    CreatedAt = u.CreatedAt
                };
            }).ToList();

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
            return OkResponse(CameraGroupResponseDto.FromMany(groups));
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

            // ===== Oldindan validatsiya (tranzaksiyani ochishdan oldin) =====
            var invalidPermissions = model.SelectedPermissions
                .Where(p => !Enum.IsDefined(typeof(Permission), p))
                .Select(p => ((int)p).ToString())
                .ToList();
            if (invalidPermissions.Count > 0)
                return FailResponse($"Noma'lum ruxsat: {string.Join(", ", invalidPermissions)}.");

            var groupIds = model.SelectedCameraGroupIds.Distinct().ToList();
            if (groupIds.Count > 0)
            {
                var existingGroupCount = await _db.CameraGroups.CountAsync(g => groupIds.Contains(g.Id));
                if (existingGroupCount != groupIds.Count)
                    return FailResponse("Tanlangan kamera guruhlaridan biri mavjud emas.");
            }

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

            // Butun jarayon bitta tranzaksiyada: rol/ruxsat/guruh biriktirilmasa
            // "yarim yaratilgan" admin qolib ketmasin.
            // DbContext EnableRetryOnFailure bilan sozlangan — shuning uchun
            // qo'lda ochilgan tranzaksiya ExecutionStrategy ichida bo'lishi SHART.
            IdentityResult? failedResult = null;
            string? failedMessage = null;

            try
            {
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    failedResult = null;
                    failedMessage = null;

                    await using var tx = await _db.Database.BeginTransactionAsync();

                    var result = await _userManager.CreateAsync(user, model.Password);
                    if (!result.Succeeded)
                    {
                        failedResult = result;
                        failedMessage = "Yaratib bo'lmadi.";
                        await tx.RollbackAsync();
                        return;
                    }

                    var roleResult = await _userManager.AddToRoleAsync(user, Roles.Admin);
                    if (!roleResult.Succeeded)
                    {
                        failedResult = roleResult;
                        failedMessage = "Rol biriktirilmadi.";
                        await tx.RollbackAsync();
                        return;
                    }

                    if (model.SelectedPermissions.Any())
                        await _permissionService.SetPermissionsAsync(user.Id, model.SelectedPermissions);

                    await SetCameraGroupsAsync(user.Id, groupIds);

                    await tx.CommitAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin yaratishda xato: {Email}", generatedEmail);
                return FailResponse("Adminni yaratib bo'lmadi.", StatusCodes.Status500InternalServerError);
            }

            if (failedResult is not null)
            {
                var errs = failedResult.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiResponse.Fail(failedMessage ?? "Yaratib bo'lmadi.", errs));
            }

            return OkResponse(new { id = user.Id, email = generatedEmail }, "Admin yaratildi.");
        }

        [HttpGet("{id}")]
        [SwaggerOperation(Summary = "Tahrirlash uchun admin ma'lumotlari")]
        public async Task<IActionResult> GetForEdit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFoundResponse("Admin topilmadi.");

            var isSuper = await _userManager.IsInRoleAsync(user, Roles.SuperAdmin);
            var perms = await _permissionService.GetPermissionsAsync(id);
            var assignedGroupIds = await _db.UserCameraGroups
                .Where(x => x.UserId == id)
                .Select(x => x.CameraGroupId)
                .ToListAsync();

            var vm = new AdminEditResponseDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                IsSuperAdmin = isSuper,
                SelectedPermissions = isSuper ? new List<Permission>() : perms.ToList(),
                SelectedCameraGroupIds = isSuper ? new List<int>() : assignedGroupIds,
                AvailableCameraGroups = CameraGroupResponseDto.FromMany(await GetCameraGroupsAsync())
            };
            return OkResponse(vm);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(Summary = "Adminni tahrirlash")]
        public async Task<IActionResult> Update(string id, [FromBody] AdminEditViewModel model)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFoundResponse("Admin topilmadi.");
            if (!ModelState.IsValid) return ValidationFail();

            var isSuper = await _userManager.IsInRoleAsync(user, Roles.SuperAdmin);
            if (isSuper)
            {
                // ToggleActive/Delete dagi himoya PUT orqali chetlab o'tilmasin.
                if (!model.IsActive)
                    return FailResponse("SuperAdmin akkauntini bloklash mumkin emas.");

                if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
                    return FailResponse("SuperAdmin emailini o'zgartirish mumkin emas.");
            }

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errs = updateResult.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiResponse.Fail("Yangilab bo'lmadi.", errs));
            }

            if (!isSuper)
            {
                await _permissionService.SetPermissionsAsync(user.Id, model.SelectedPermissions);
                await SetCameraGroupsAsync(user.Id, model.SelectedCameraGroupIds);

                // Bloklangan/huquqi o'zgargan admin eski JWT bilan ishlayvermasin.
                await _userManager.UpdateSecurityStampAsync(user);
            }

            return OkResponse("Admin yangilandi.");
        }

        [HttpPost("{id}/reset-password")]
        [SwaggerOperation(Summary = "Admin parolini qayta o'rnatish")]
        public async Task<IActionResult> ResetPassword(string id, [FromBody] AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFoundResponse("Admin topilmadi.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!result.Succeeded)
            {
                var errs = result.Errors.GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiResponse.Fail("Parol o'zgartirilmadi.", errs));
            }

            return OkResponse("Parol o'zgartirildi.");
        }

        [HttpPost("{id}/toggle-active")]
        [SwaggerOperation(Summary = "Admin akkauntini faollashtirish/bloklash")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFoundResponse("Admin topilmadi.");

            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                return FailResponse("SuperAdmin akkauntini bloklash mumkin emas.");

            user.IsActive = !user.IsActive;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return FailResponse("Holatni o'zgartirib bo'lmadi.");

            // Bloklangan admin tokeni darhol bekor bo'lsin.
            await _userManager.UpdateSecurityStampAsync(user);

            return OkResponse(new { isActive = user.IsActive });
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(Summary = "Adminni o'chirish")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFoundResponse("Admin topilmadi.");

            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                return FailResponse("SuperAdmin akkauntini o'chirish mumkin emas.");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return FailResponse("Adminni o'chirib bo'lmadi.");

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
