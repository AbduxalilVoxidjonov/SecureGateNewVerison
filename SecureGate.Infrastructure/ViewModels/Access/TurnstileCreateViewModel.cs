using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Access
{
    public class TurnstileCreateViewModel
    {
        [Required]
        [Display(Name = "Nomi")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Joylashuv")]
        public string? Location { get; set; }

        [Display(Name = "Model")]
        public TurnstileModel Model { get; set; }

        [Display(Name = "IP manzil")]
        public string? IpAddress { get; set; }

        [Display(Name = "Port")]
        public int Port { get; set; } = 4370;

        [Display(Name = "Tur")]
        public TurnstileType Type { get; set; }

        [Display(Name = "Yo'nalish")]
        public TurnstileDirection Direction { get; set; }

        [Display(Name = "Biriktirilgan kamera")]
        public int? LinkedCameraId { get; set; }

        public bool FaceRecognitionEnabled { get; set; } = true;
        public bool RfidEnabled { get; set; } = true;
        public bool QrCodeEnabled { get; set; }

        public List<Camera> AvailableCameras { get; set; } = new();
    }
}
