using Microsoft.AspNetCore.Authorization;
using SecureGate.Domain.Auth;

namespace SecureGate.Api.Filters
{
    /// <summary>
    /// API endpoint uchun: Permission'ga ega foydalanuvchilarga ruxsat beradi.
    /// SuperAdmin barcha permission'lardan o'tadi.
    /// </summary>
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(Permission permission)
            : base(policy: PolicyName(permission))
        {
        }

        public static string PolicyName(Permission permission) => $"Perm:{permission}";
    }

    /// <summary>
    /// Faqat SuperAdmin.
    /// </summary>
    public class SuperAdminOnlyAttribute : AuthorizeAttribute
    {
        public SuperAdminOnlyAttribute() : base()
        {
            Roles = SecureGate.Domain.Auth.Roles.SuperAdmin;
        }
    }
}
