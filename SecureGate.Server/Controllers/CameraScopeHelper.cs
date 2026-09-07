using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain.Auth;
using System.Security.Claims;

namespace SecureGate.Api.Controllers
{
    /// <summary>
    /// Kamera guruhlari bo'yicha admin scope'ini controller darajasida qo'llash.
    /// CameraService ichidagi mantiq bilan bir xil semantika:
    ///   null qaytsa — cheklov yo'q (SuperAdmin, yoki adminga guruh biriktirilmagan);
    ///   ro'yxat qaytsa — faqat shu guruh ID'lari ruxsat etilgan.
    /// </summary>
    public static class CameraScopeHelper
    {
        public static async Task<List<int>?> GetAllowedGroupIdsAsync(AppDbContext db, ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true) return null;
            if (user.IsInRole(Roles.SuperAdmin)) return null;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;

            var assigned = await db.UserCameraGroups
                .Where(x => x.UserId == userId)
                .Select(x => x.CameraGroupId)
                .ToListAsync();

            return assigned.Count == 0 ? null : assigned;
        }

        public static bool IsGroupAllowed(List<int>? allowedGroupIds, int groupId)
            => allowedGroupIds is null || allowedGroupIds.Contains(groupId);

        /// <summary>
        /// Berilgan kamera ID'lari ruxsat etilgan guruhlarga tegishlimi?
        /// Guruhsiz (CameraGroupId == null) kamera cheklangan admin uchun taqiqlanadi.
        /// </summary>
        public static async Task<bool> AreCamerasAllowedAsync(
            AppDbContext db, List<int>? allowedGroupIds, IEnumerable<int> cameraIds)
        {
            if (allowedGroupIds is null) return true;

            var ids = cameraIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return true;

            var matching = await db.Cameras
                .Where(c => ids.Contains(c.Id)
                            && c.CameraGroupId != null
                            && allowedGroupIds.Contains(c.CameraGroupId.Value))
                .CountAsync();

            return matching == ids.Count;
        }

        /// <summary>Ruxsat etilgan guruhlardagi kamera ID'lari (cheklov bo'lmasa null).</summary>
        public static async Task<HashSet<int>?> GetAllowedCameraIdsAsync(AppDbContext db, List<int>? allowedGroupIds)
        {
            if (allowedGroupIds is null) return null;

            var ids = await db.Cameras
                .Where(c => c.CameraGroupId != null && allowedGroupIds.Contains(c.CameraGroupId.Value))
                .Select(c => c.Id)
                .ToListAsync();

            return ids.ToHashSet();
        }
    }
}
