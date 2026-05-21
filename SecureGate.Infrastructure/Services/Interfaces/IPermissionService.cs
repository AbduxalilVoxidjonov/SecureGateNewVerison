using SecureGate.Domain.Auth;
using System.Security.Claims;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, Permission permission);
        Task<bool> HasPermissionAsync(string userId, Permission permission);
        Task<IReadOnlyList<Permission>> GetPermissionsAsync(string userId);
        Task SetPermissionsAsync(string userId, IEnumerable<Permission> permissions);
    }
}
