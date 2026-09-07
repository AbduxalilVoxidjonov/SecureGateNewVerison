using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    public class CameraCreateViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Kamera nomi majburiy")]
        [StringLength(100, ErrorMessage = "Nom 100 belgidan oshmasligi kerak")]
        [Display(Name = "Kamera nomi")]
        public string Name { get; set; } = string.Empty;

        // Turniket â€” yuz tanilsa AccessLog'ga yozadi va turniketni ochadi.
        // Oddiy â€” yuz tanilsa faqat CameraUser ro'yxatiga yozadi.
        [Display(Name = "Kamera turi")]
        public CameraType Type { get; set; } = CameraType.Turnstile;

        [Display(Name = "Protokol")]
        public CameraProtocol Protocol { get; set; }

        [Display(Name = "Model")]
        public CameraModel CameraModel { get; set; }

        // Stream URL formati: rtsp://, rtmp://, http(s):// dan boshlanishi shart.
        [Display(Name = "Stream URL (main — yozib olish uchun)")]
        [StringLength(500)]
        [RegularExpression(@"^(rtsp|rtmps?|rtmp|https?)://\S+$",
            ErrorMessage = "Stream URL rtsp://, rtmp://, http:// yoki https:// dan boshlanishi kerak")]
        public string? StreamUrl { get; set; }

        /// <summary>
        /// AI uchun sub-stream URL (480p/720p) — yuz tanish CPU/GPU yukini 4-6 marta kamaytiradi.
        /// Misol (Hikvision): rtsp://cam/Streaming/Channels/102
        /// </summary>
        [Display(Name = "AI Stream URL (sub-stream — yuz tanish uchun, ixtiyoriy)")]
        [StringLength(500)]
        [RegularExpression(@"^(rtsp|rtmps?|rtmp|https?)://\S+$",
            ErrorMessage = "AI Stream URL rtsp://, rtmp://, http:// yoki https:// dan boshlanishi kerak")]
        public string? AiStreamUrl { get; set; }

        // IPv4 yoki hostname (192.168.1.10 yoki cam-1.local)
        /// <summary>
        /// Qurilma turi: to'g'ridan-to'g'ri IP-kamera yoki NVR ning bitta kanali.
        /// NvrChannel bo'lsa IpAddress/Port — NVR manzili, ChannelNumber esa kanal raqami.
        /// </summary>
        [Display(Name = "Qurilma turi")]
        public DeviceKind DeviceKind { get; set; } = DeviceKind.Camera;

        /// <summary>NVR kanal raqami (1 dan boshlab). DeviceKind == NvrChannel bo'lganda majburiy.</summary>
        [Display(Name = "NVR kanal raqami")]
        [Range(1, 256, ErrorMessage = "Kanal raqami 1 dan 256 gacha bo'lishi kerak")]
        public int? ChannelNumber { get; set; }

        [Display(Name = "IP manzil")]
        [StringLength(100)]
        [RegularExpression(@"^([a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?$",
            ErrorMessage = "IP yoki hostname formati noto'g'ri (masalan: 192.168.1.10 yoki cam.local)")]
        public string? IpAddress { get; set; }

        [Display(Name = "Port")]
        [Range(1, 65535, ErrorMessage = "Port 1 dan 65535 gacha bo'lishi kerak")]
        public int Port { get; set; } = 554;

        [Display(Name = "Login")]
        [StringLength(50)]
        public string? Username { get; set; }

        [Display(Name = "Parol")]
        [StringLength(200)]
        public string? Password { get; set; }

        [Display(Name = "Guruh")]
        public int? CameraGroupId { get; set; }

        [Display(Name = "Sifat")]
        public VideoQuality Quality { get; set; }

        public bool FaceRecognitionEnabled { get; set; } = true;
        public bool ContinuousRecording { get; set; } = true;
        public bool MotionDetection { get; set; }

        public List<CameraGroup> AvailableGroups { get; set; } = new();

        // ===== NVR kanali uchun qo'shimcha qoidalar =====
        // [ApiController] avtomatik model validatsiyasi IValidatableObject'ni ham chaqiradi,
        // shu sababli buzilgan holat controller'ga yetib bormaydi (400 qaytadi).
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DeviceKind == DeviceKind.NvrChannel)
            {
                if (!ChannelNumber.HasValue || ChannelNumber.Value < 1)
                    yield return new ValidationResult(
                        "NVR kanali uchun kanal raqami majburiy",
                        new[] { nameof(ChannelNumber) });

                if (string.IsNullOrWhiteSpace(IpAddress))
                    yield return new ValidationResult(
                        "NVR kanali uchun NVR IP manzili majburiy",
                        new[] { nameof(IpAddress) });
            }
        }
    }
}
