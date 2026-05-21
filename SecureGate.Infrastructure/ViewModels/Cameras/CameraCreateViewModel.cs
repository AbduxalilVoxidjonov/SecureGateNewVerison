using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    public class CameraCreateViewModel
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
    }
}
