using Microsoft.AspNetCore.Http;
using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.People
{
    // ==================== STAFF ====================
    public class StaffCreateViewModel
    {
        [Display(Name = "Yuz rasmi")]
        public IFormFile? PhotoFile { get; set; }

        // Veb-kamera orqali olingan rasm (base64 data URL: "data:image/png;base64,....")
        public string? CapturedPhotoBase64 { get; set; }

        [Required]
        [Display(Name = "F.I.O")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Lavozim")]
        public string Position { get; set; } = string.Empty;

        [Display(Name = "Bo'lim")]
        public Department Department { get; set; }

        [Display(Name = "Smena")]
        public ShiftType Shift { get; set; }

        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Display(Name = "Kirish darajasi")]
        public AccessLevel AccessLevel { get; set; }
    }
}
