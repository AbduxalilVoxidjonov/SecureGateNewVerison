using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using System.Security.Claims;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public PermissionService(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, Permission permission)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (user.IsInRole(Roles.SuperAdmin)) return true;

            var userId = _userManager.GetUserId(user);
            if (string.IsNullOrEmpty(userId)) return false;

            return await HasPermissionAsync(userId, permission);
        }

        public async Task<bool> HasPermissionAsync(string userId, Permission permission)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive) return false;
            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin)) return true;

            return await _db.UserPermissions
                .AnyAsync(p => p.UserId == userId && p.Permission == permission);
        }

        public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Array.Empty<Permission>();

            if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
                return Enum.GetValues<Permission>();

            return await _db.UserPermissions
                .Where(p => p.UserId == userId)
                .Select(p => p.Permission)
                .ToListAsync();
        }

        public async Task SetPermissionsAsync(string userId, IEnumerable<Permission> permissions)
        {
            var existing = await _db.UserPermissions
                .Where(p => p.UserId == userId)
                .ToListAsync();

            _db.UserPermissions.RemoveRange(existing);

            var toAdd = permissions.Distinct().Select(p => new UserPermission
            {
                UserId = userId,
                Permission = p
            });

            await _db.UserPermissions.AddRangeAsync(toAdd);
            await _db.SaveChangesAsync();
        }
    }
}
