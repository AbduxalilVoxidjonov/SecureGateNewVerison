namespace SecureGate.Api.Auth
{
    /// <summary>
    /// Attribute argumentlari uchun const string sxema nomlari.
    /// JwtBearerDefaults.AuthenticationScheme = "Bearer"
    /// IdentityConstants.ApplicationScheme = "Identity.Application"
    /// </summary>
    public static class AuthSchemes
    {
        public const string Jwt = "Bearer";
        public const string Cookie = "Identity.Application";
        public const string JwtAndCookie = Jwt + "," + Cookie;
    }
}
