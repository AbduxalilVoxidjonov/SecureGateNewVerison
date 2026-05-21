using SecureGate.Domain.Auth;

namespace SecureGate.Api.Auth
{
    public interface ITokenService
    {
        Task<TokenResult> CreateAccessTokenAsync(AppUser user);
        string CreateRefreshToken();
    }

    public class TokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    }
}
