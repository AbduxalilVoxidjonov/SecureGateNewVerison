using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("fullName", user.FullName ?? string.Empty),
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var perm in permissions)
                claims.Add(new Claim("permission", perm));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return new TokenResult
            {
                AccessToken = jwt,
                ExpiresAt = expires,
                TokenType = "Bearer",
                Roles = roles.ToList(),
                Permissions = permissions
            };
        }

        public string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }
    }
}
