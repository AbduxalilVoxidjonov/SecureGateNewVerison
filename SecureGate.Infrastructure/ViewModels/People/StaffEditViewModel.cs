using Microsoft.AspNetCore.Http;
using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.People
{
    public class StaffEditViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Yangi yuz rasmi")]
        public IFormFile? PhotoFile { get; set; }

        public string? CapturedPhotoBase64 { get; set; }

        [Display(Name = "Joriy rasm")]
        public string? PhotoPath { get; set; }

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

        [Display(Name = "Holat")]
        public StaffStatus Status { get; set; }

        [Display(Name = "Yuz tanish")]
        public bool FaceRecognitionEnabled { get; set; }
    }
}
