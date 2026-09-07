using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Auth;
using SecureGate.Api.Models;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using System.IdentityModel.Tokens.Jwt;

namespace SecureGate.Api.Controllers
{
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        // User enumeration'ning oldini olish: mavjud bo'lmagan email uchun ham
        // parol tekshiruvi bilan taxminan bir xil vaqt sarflanadi.
        private static readonly string DummyPasswordHash =
            new PasswordHasher<AppUser>().HashPassword(new AppUser(), "SecureGate$Dummy#Password1");

        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            SignInManager<AppUser> signInManager,
            UserManager<AppUser> userManager,
            ITokenService tokenService,
            IPermissionService permissionService,
            ILogger<AuthController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _tokenService = tokenService;
            _permissionService = permissionService;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Email/parol orqali kirish", Description = "JWT access + refresh token qaytaradi. RememberMe=true bo'lsa qo'shimcha cookie ham o'rnatiladi.")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return ValidationFail();

            const string invalidMessage = "Email yoki parol noto'g'ri.";

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                // Timing'ni tenglashtirish uchun soxta hash tekshiruvi.
                _userManager.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyPasswordHash, request.Password);
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);
            }

            // lockoutOnFailure: true — cheksiz brute-force'ning oldini oladi
            // (Identity Lockout sozlamalari Program.cs da: 5 urinish / 15 daqiqa).
            var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (signIn.IsLockedOut)
            {
                _logger.LogWarning("Login bloklandi (lockout): {UserId}", user.Id);
                return FailResponse("Akkaunt vaqtincha bloklandi. Birozdan so'ng qayta urinib ko'ring.",
                    StatusCodes.Status423Locked);
            }

            if (!signIn.Succeeded)
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            if (!user.IsActive)
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            // JWT
            var token = await _tokenService.CreateAccessTokenAsync(user);
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user);

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
                RefreshToken = refreshToken,
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
        [SwaggerOperation(Summary = "Tizimdan chiqish", Description = "SecurityStamp yangilanadi — barcha eski access/refresh tokenlar darhol bekor bo'ladi.")]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Barcha mavjud JWT (access + refresh) tokenlarni bekor qiladi.
                await _userManager.UpdateSecurityStampAsync(user);
            }

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
        [SwaggerOperation(Summary = "Joriy foydalanuvchi parolini o'zgartirish", Description = "Muvaffaqiyatli bo'lsa eski tokenlar bekor bo'ladi — qaytadan login qilish kerak.")]
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

            // Parol o'zgargach eski tokenlar ishlamasligi kerak.
            await _userManager.UpdateSecurityStampAsync(user);

            return OkResponse("Parol o'zgartirildi. Iltimos, qaytadan tizimga kiring.");
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        [SwaggerOperation(
            Summary = "Refresh token bilan yangi access token olish",
            Description = "Foydalanuvchi FAQAT imzolangan refresh token ichidan aniqlanadi. " +
                          "Har chaqiruvda refresh token ham yangilanadi (rotatsiya).")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid) return ValidationFail();

            const string invalidMessage = "Refresh token yaroqsiz yoki muddati o'tgan.";

            // (a) Imzo / issuer / audience / muddat — to'liq tekshiruv
            var principal = _tokenService.ValidateRefreshToken(request.RefreshToken);
            if (principal == null)
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            // (b) Token turi aynan "refresh" bo'lishi shart (access token bilan almashtirib bo'lmasin)
            var tokenType = principal.FindFirst(SecureGateClaims.TokenType)?.Value;
            if (!string.Equals(tokenType, SecureGateClaims.RefreshTokenType, StringComparison.Ordinal))
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            // (c) Foydalanuvchi faqat token ichidagi sub'dan aniqlanadi
            var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);

            // (d) SecurityStamp — logout / parol o'zgarishi tokenni bekor qiladi
            var tokenStamp = principal.FindFirst(SecureGateClaims.SecurityStamp)?.Value;
            var currentStamp = await _userManager.GetSecurityStampAsync(user);
            if (string.IsNullOrEmpty(tokenStamp) ||
                !string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal))
            {
                _logger.LogWarning("Bekor qilingan refresh token ishlatildi: {UserId}", user.Id);
                return FailResponse(invalidMessage, StatusCodes.Status401Unauthorized);
            }

            // (e) Bloklangan akkaunt
            if (!user.IsActive)
                return FailResponse("Akkaunt bloklangan.", StatusCodes.Status401Unauthorized);

            // (f) Yangi access + yangi refresh (rotatsiya)
            var token = await _tokenService.CreateAccessTokenAsync(user);
            var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(user);

            return OkResponse(new RefreshTokenResponse
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType,
                ExpiresAt = token.ExpiresAt,
                RefreshToken = newRefreshToken
            });
        }
    }
}
