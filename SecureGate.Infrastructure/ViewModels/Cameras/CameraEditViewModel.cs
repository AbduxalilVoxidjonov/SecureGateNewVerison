using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    public class CameraEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Kamera kodi majburiy")]
        [StringLength(20)]
        [Display(Name = "Kamera kodi")]
        public string CameraCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nomi majburiy")]
        [StringLength(100)]
        [Display(Name = "Kamera nomi")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Stream URL (main — yozib olish uchun)")]
        [StringLength(500)]
        [RegularExpression(@"^(rtsp|rtmps?|rtmp|https?)://\S+$",
            ErrorMessage = "Stream URL rtsp://, rtmp://, http:// yoki https:// dan boshlanishi kerak")]
        public string? StreamUrl { get; set; }

        /// <summary>
        /// AI sub-stream URL (480p/720p). Yuz tanish uchun ishlatiladi.
        /// Mavjud bo'lsa — CameraStreamWorker buni ishlatadi.
        /// </summary>
        [Display(Name = "AI Stream URL (sub-stream)")]
        [StringLength(500)]
        [RegularExpression(@"^(rtsp|rtmps?|rtmp|https?)://\S+$",
            ErrorMessage = "AI Stream URL rtsp://, rtmp://, http:// yoki https:// dan boshlanishi kerak")]
        public string? AiStreamUrl { get; set; }

        [Display(Name = "IP manzil")]
        [StringLength(100)]
        [RegularExpression(@"^([a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)*[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?$",
            ErrorMessage = "IP yoki hostname formati noto'g'ri (masalan: 192.168.1.10 yoki cam.local)")]
        public string? IpAddress { get; set; }

        [Display(Name = "Port")]
        [Range(1, 65535, ErrorMessage = "Port 1 dan 65535 gacha bo'lishi kerak")]
        public int Port { get; set; }

        [Display(Name = "Login")]
        [StringLength(50)]
        public string? Username { get; set; }

        // Edit oqimida bo'sh qoldirilsa â€” eski parol saqlanadi.
        [Display(Name = "Parol")]
        [StringLength(200)]
        public string? Password { get; set; }

        public CameraProtocol Protocol { get; set; }
        public CameraModel CameraModel { get; set; }
        public VideoQuality Quality { get; set; }
        public CameraStatus Status { get; set; }

        [Display(Name = "Kamera turi")]
        public CameraType Type { get; set; }

        public bool FaceRecognitionEnabled { get; set; }
        public bool ContinuousRecording { get; set; }
        public bool MotionDetection { get; set; }

        [Range(1, 60, ErrorMessage = "FPS 1 dan 60 gacha bo'lishi kerak")]
        public int Fps { get; set; }

        public int? CameraGroupId { get; set; }
        public List<CameraGroup> AvailableGroups { get; set; } = new();
    }
}
