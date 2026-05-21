namespace SecureGate.Api.Auth
{
    public class JwtSettings
    {
        public string Issuer { get; set; } = "SecureGate.Api";
        public string Audience { get; set; } = "SecureGate.Clients";
        public string Key { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 60;
        public int RefreshTokenDays { get; set; } = 14;
    }
}
