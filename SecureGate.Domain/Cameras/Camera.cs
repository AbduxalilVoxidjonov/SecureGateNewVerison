using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SecureGate.Domain;

namespace SecureGate.Domain.Cameras
{
    public class Camera
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Kamera ID")]
        public string CameraCode { get; set; } = string.Empty; // CAM-01, CAM-02...

        [Required]
        [StringLength(100)]
        [Display(Name = "Nomi")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Main stream URL — yozib olish va to'liq sifatda ko'rsatish uchun (FullHD/4K).</summary>
        [Display(Name = "Stream URL (main)")]
        public string? StreamUrl { get; set; }

        /// <summary>
        /// AI sub-stream URL — yuz tanish uchun pasaytirilgan sifatdagi oqim (480p/720p).
        /// Mavjud bo'lsa — CameraStreamWorker buni ishlatadi. Yo'q bo'lsa — StreamUrl'ga fallback.
        /// 500+ kamerali deployment'larda CPU/GPU yukini 4-6 marta kamaytiradi.
        /// Misol (Hikvision sub-stream): rtsp://cam/Streaming/Channels/102
        /// </summary>
        [Display(Name = "AI Stream URL (sub-stream)")]
        public string? AiStreamUrl { get; set; }

        [Display(Name = "IP manzil")]
        public string? IpAddress { get; set; }

        [Display(Name = "Port")]
        public int Port { get; set; } = 554;

        [Display(Name = "Login")]
        public string? Username { get; set; }

        [Display(Name = "Parol")]
        public string? Password { get; set; }

        [Display(Name = "Protokol")]
        public CameraProtocol Protocol { get; set; } = CameraProtocol.RTSP;

        [Display(Name = "Model")]
        public CameraModel CameraModel { get; set; } = CameraModel.Hikvision;

        [Display(Name = "Sifat")]
        public VideoQuality Quality { get; set; } = VideoQuality.FullHD;

        [Display(Name = "Holat")]
        public CameraStatus Status { get; set; } = CameraStatus.Online;

        // Kamera turi:
        //   Turnstile — turniketni boshqaradi, kirish/chiqishni AccessLog'ga yozadi
        //   Regular   — faqat kuzatadi, ko'ringan odamlarni CameraUser jadvaliga yozadi
        [Display(Name = "Kamera turi")]
        public CameraType Type { get; set; } = CameraType.Turnstile;

        [Display(Name = "Yuzni tanish")]
        public bool FaceRecognitionEnabled { get; set; } = true;

        [Display(Name = "24/7 yozib olish")]
        public bool ContinuousRecording { get; set; } = true;

        [Display(Name = "Harakat aniqlash")]
        public bool MotionDetection { get; set; }

        [Display(Name = "FPS")]
        public int Fps { get; set; } = 30;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public int? CameraGroupId { get; set; }

        [ForeignKey("CameraGroupId")]
        public CameraGroup? CameraGroup { get; set; }

        public ICollection<Turnstile> LinkedTurnstiles { get; set; } = new List<Turnstile>();

        [NotMapped]
        public string StatusMeta => $"{Quality.GetDisplayName()} · {Fps}fps{(FaceRecognitionEnabled ? " · AI ON" : "")}";
    }
}