using Microsoft.AspNetCore.Http;
using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.People
{
    public class UsersCreateViewModel
    {
        [Display(Name = "Yuz rasmi")]
        public IFormFile? PhotoFile { get; set; }

        // Veb-kamera orqali olingan rasm (base64 data URL: "data:image/png;base64,....")
        public string? CapturedPhotoBase64 { get; set; }

        [Required(ErrorMessage = "Ism kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Ism")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Familiya kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Familiya")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Tug'ilgan sana")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Jinsi")]
        public Gender Gender { get; set; }

        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Phone]
        [Display(Name = "Ota-ona telefoni")]
        public string? ParentPhone { get; set; }

        [Display(Name = "Manzil")]
        public string? Address { get; set; }

        [Display(Name = "Guruh")]
        public int? GroupId { get; set; }

        [Display(Name = "Yuz tanish")]
        public bool FaceRecognitionEnabled { get; set; } = true;

        [Display(Name = "SMS bildirishnoma")]
        public bool SmsNotification { get; set; }

        // Ruxsat etilgan turniketlar
        public List<int> AllowedTurnstileIds { get; set; } = new();

        // Dropdown uchun
        public List<Turnstile> AvailableTurnstiles { get; set; } = new();
    }

 
}
