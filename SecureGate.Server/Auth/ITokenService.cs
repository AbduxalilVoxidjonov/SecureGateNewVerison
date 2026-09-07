using SecureGate.Domain.Auth;
using System.Security.Claims;

namespace SecureGate.Api.Auth
{
    public interface ITokenService
    {
        Task<TokenResult> CreateAccessTokenAsync(AppUser user);

        /// <summary>
        /// Imzolangan (stateless) refresh token yaratadi: typ=refresh, sub=userId, security_stamp.
        /// </summary>
        Task<string> CreateRefreshTokenAsync(AppUser user);

        /// <summary>
        /// Refresh tokenni to'liq tekshiradi (imzo, issuer, audience, muddat).
        /// Yaroqsiz bo'lsa null qaytaradi. Claim'lar xom holda (mapping'siz) qaytadi.
        /// </summary>
        ClaimsPrincipal? ValidateRefreshToken(string? refreshToken);
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
