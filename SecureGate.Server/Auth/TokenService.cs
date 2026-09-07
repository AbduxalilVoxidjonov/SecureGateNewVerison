using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SecureGate.Api.Auth
{
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _settings;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPermissionService _permissionService;

        public TokenService(
            IOptions<JwtSettings> settings,
            UserManager<AppUser> userManager,
            IPermissionService permissionService)
        {
            _settings = settings.Value;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        public async Task<TokenResult> CreateAccessTokenAsync(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var isSuperAdmin = roles.Contains(Roles.SuperAdmin);

            var permissions = isSuperAdmin
                ? Enum.GetValues<Permission>().Select(p => p.ToString()).ToList()
                : (await _permissionService.GetPermissionsAsync(user.Id))
                    .Select(p => p.ToString())
                    .ToList();

            var securityStamp = await _userManager.GetSecurityStampAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("fullName", user.FullName ?? string.Empty),
                new(SecureGateClaims.TokenType, SecureGateClaims.AccessTokenType),
                new(SecureGateClaims.SecurityStamp, securityStamp ?? string.Empty),
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var perm in permissions)
                claims.Add(new Claim("permission", perm));

            var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
            var jwt = WriteToken(claims, expires);

            return new TokenResult
            {
                AccessToken = jwt,
                ExpiresAt = expires,
                TokenType = "Bearer",
                Roles = roles.ToList(),
                Permissions = permissions
            };
        }

        public async Task<string> CreateRefreshTokenAsync(AppUser user)
        {
            var securityStamp = await _userManager.GetSecurityStampAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(SecureGateClaims.TokenType, SecureGateClaims.RefreshTokenType),
                new(SecureGateClaims.SecurityStamp, securityStamp ?? string.Empty),
            };

            var expires = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays);
            return WriteToken(claims, expires);
        }

        public ClaimsPrincipal? ValidateRefreshToken(string? refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var handler = new JwtSecurityTokenHandler();
            // Claim nomlari o'zgarmasin (sub -> nameidentifier mapping'ini o'chiramiz)
            handler.InboundClaimTypeMap.Clear();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidIssuer = _settings.Issuer,
                ValidAudience = _settings.Audience,
                IssuerSigningKey = SigningKey(),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            try
            {
                var principal = handler.ValidateToken(refreshToken, parameters, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwt ||
                    !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception)
            {
                // Yaroqsiz imzo / muddati o'tgan / buzilgan token
                return null;
            }
        }

        private SymmetricSecurityKey SigningKey() =>
            new(Encoding.UTF8.GetBytes(_settings.Key));

        private string WriteToken(IEnumerable<Claim> claims, DateTime expiresUtc)
        {
            var creds = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresUtc,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
