using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using SecureGate.Domain;

namespace SecureGate.Domain.Auth
{
    public class AppUser : IdentityUser
    {
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Admin akkaunt qaysi xodimga tegishli (ixtiyoriy — SuperAdmin biriktirilmagan bo'lishi mumkin)
        public int? StaffId { get; set; }

        [ForeignKey(nameof(StaffId))]
        public Staff? Staff { get; set; }

        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();

        // Admin qaysi kamera guruhlarini ko'rishi mumkinligi. Bo'sh bo'lsa — hammasi.
        public ICollection<UserCameraGroup> CameraGroups { get; set; } = new List<UserCameraGroup>();
    }
}
