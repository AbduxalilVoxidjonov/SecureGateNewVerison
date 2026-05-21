using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Auth;
using SecureGate.Api.Models;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace SecureGate.Api.Controllers
{
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthController : ApiControllerBase
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPermissionService _permissionService;

        public AuthController(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IPermissionService permissionService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _tokenService = tokenService;
            _permissionService = permissionService;
        }

        [HttpPost("login")]
        [SwaggerOperation(Summary = "Email/parol orqali kirish", Description = "JWT access token qaytaradi. RememberMe=true bo'lsa qo'shimcha cookie ham o'rnatiladi.")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
                return FailResponse("Email yoki parol noto'g'ri.", StatusCodes.Status401Unauthorized);

            var passwordOk = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordOk)
                return FailResponse("Email yoki parol noto'g'ri.", StatusCodes.Status401Unauthorized);

            // JWT
            var token = await _tokenService.CreateAccessTokenAsync(user);

            // Cookie (parallel sxema) — agar RememberMe so'ralsa
            if (request.RememberMe)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isSuper = roles.Contains(Roles.SuperAdmin);
            var perms = isSuper
                ? Enum.GetValues<Permission>().Select(p => p.ToString()).ToList()
                : (await _permissionService.GetPermissionsAsync(user.Id)).Select(p => p.ToString()).ToList();

            return OkResponse(new LoginResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresAt = token.ExpiresAt,
                RefreshToken = _tokenService.CreateRefreshToken(),
                User = new UserInfoDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    IsActive = user.IsActive,
                    IsSuperAdmin = isSuper,
                    Roles = roles.ToList(),
                    Permissions = perms
                }
            });
        }

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = AuthSchemes.JwtAndCookie)]
        [SwaggerOperation(Summary = "Tizimdan chiqish")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return OkResponse("Tizimdan chiqildi.");
        }

        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = AuthSchemes.JwtAndCookie)]
        [SwaggerOperation(Summary = "Hozirgi foydalanuvchi ma'lumotlari")]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return FailResponse("Foydalanuvchi topilmadi.", StatusCodes.Status401Unauthorized);

            var roles = await _userManager.GetRolesAsync(user);
            var isSuper = roles.Contains(Roles.SuperAdmin);
            var perms = isSuper
                ? Enum.GetValues<Permission>().Select(p => p.ToString()).ToList()
                : (await _permissionService.GetPermissionsAsync(user.Id)).Select(p => p.ToString()).ToList();

            return OkResponse(new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                IsActive = user.IsActive,
                IsSuperAdmin = isSuper,
                Roles = roles.ToList(),
                Permissions = perms
            });
        }

        [HttpPost("change-password")]
        [Authorize(AuthenticationSchemes = AuthSchemes.JwtAndCookie)]
        [SwaggerOperation(Summary = "Joriy foydalanuvchi parolini o'zgartirish")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid) return ValidationFail();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return FailResponse("Foydalanuvchi topilmadi.", StatusCodes.Status401Unauthorized);

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
                return BadRequest(ApiResponse.Fail("Parolni o'zgartirib bo'lmadi.", errors));
            }

            return OkResponse("Parol o'zgartirildi.");
        }

        [HttpPost("refresh")]
        [SwaggerOperation(Summary = "Refresh token bilan yangi access token olish", Description = "Hozirda placeholder — refresh token saqlash uchun DB jadval qo'shilishi kerak. Hozircha mavjud foydalanuvchi uchun yangi token yaratadi.")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return FailResponse("Email majburiy.");

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !user.IsActive)
                return FailResponse("Foydalanuvchi topilmadi yoki bloklangan.", StatusCodes.Status401Unauthorized);

            var token = await _tokenService.CreateAccessTokenAsync(user);

            return OkResponse(new
            {
                accessToken = token.AccessToken,
                tokenType = token.TokenType,
                expiresAt = token.ExpiresAt,
                refreshToken = _tokenService.CreateRefreshToken()
            });
        }
    }

    public class RefreshTokenRequest
    {
        public string Email { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
