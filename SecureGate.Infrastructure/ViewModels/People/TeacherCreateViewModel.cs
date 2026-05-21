using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.People
{
    // ==================== TEACHER ====================
    public class TeacherCreateViewModel
    {
        [Required(ErrorMessage = "F.I.O kiritish shart")]
        [StringLength(100)]
        [Display(Name = "F.I.O")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Fan")]
        public string Subject { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Tajriba (yil)")]
        public int ExperienceYears { get; set; }

        [Display(Name = "Lavozim")]
        public TeacherPosition Position { get; set; }

        public string? GroupIds { get; set; } // "10-A, 10-B, 11-A"
    }
}
