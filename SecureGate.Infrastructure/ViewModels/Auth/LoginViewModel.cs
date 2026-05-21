using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Auth
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email majburiy")]
        [EmailAddress(ErrorMessage = "Email noto'g'ri formatda")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parol majburiy")]
        [DataType(DataType.Password)]
        [Display(Name = "Parol")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Eslab qol")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
