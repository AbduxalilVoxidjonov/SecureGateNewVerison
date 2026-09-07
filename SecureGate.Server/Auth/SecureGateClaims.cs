namespace SecureGate.Api.Auth
{
    /// <summary>
    /// SecureGate tokenlaridagi maxsus claim nomlari.
    /// </summary>
    public static class SecureGateClaims
    {
        /// <summary>Identity SecurityStamp — logout / parol o'zgarishida tokenni bekor qilish uchun.</summary>
        public const string SecurityStamp = "security_stamp";

        /// <summary>Token turi: "access" yoki "refresh".</summary>
        public const string TokenType = "typ";

        public const string AccessTokenType = "access";
        public const string RefreshTokenType = "refresh";
    }
}
