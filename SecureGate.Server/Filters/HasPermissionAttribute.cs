using Microsoft.AspNetCore.Authorization;
using SecureGate.Api.Auth;
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
            // Policy'ning o'zida ikkala sxema ro'yxatdan o'tgan (Program.cs), lekin
            // izchillik uchun bu yerda ham aniq ko'rsatamiz.
            AuthenticationSchemes = AuthSchemes.JwtAndCookie;
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
            // Sxema ko'rsatilmasa faqat DefaultAuthenticateScheme (JWT) ishlaydi va
            // cookie bilan kirgan SuperAdmin 401 oladi.
            AuthenticationSchemes = AuthSchemes.JwtAndCookie;
        }
    }
}
