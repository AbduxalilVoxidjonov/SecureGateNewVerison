using System.ComponentModel.DataAnnotations;

namespace SecureGate.Api.Models
{
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        /// <summary>true bo'lsa, cookie ham o'rnatiladi va "remember me" sifatida amal qiladi.</summary>
        public bool RememberMe { get; set; }
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfoDto User { get; set; } = null!;
    }

    public class UserInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    }

    public class ChangePasswordRequest
    {
        [Required] public string CurrentPassword { get; set; } = string.Empty;
        [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Refresh so'rovi. Foydalanuvchi FAQAT token ichidan aniqlanadi —
    /// hech qanday email/userId qabul qilinmaydi (autentifikatsiya bypass'ining oldini olish).
    /// </summary>
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "Refresh token majburiy.")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>Favqulodda holatda barcha turniketlarni ochish uchun majburiy sabab.</summary>
    public class EmergencyOpenRequest
    {
        [Required(ErrorMessage = "Sabab ko'rsatilishi majburiy.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Sabab 5-500 belgi oralig'ida bo'lishi kerak.")]
        public string Reason { get; set; } = string.Empty;
    }
}
