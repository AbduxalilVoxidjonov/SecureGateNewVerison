using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Access
{
    public class Turnstile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Nomi")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Joylashuv")]
        [StringLength(200)]
        public string? Location { get; set; }

        [Display(Name = "IP manzil")]
        [StringLength(45)]
        public string? IpAddress { get; set; }

        [Display(Name = "Port")]
        public int Port { get; set; } = 4370;

        [Display(Name = "Model")]
        public TurnstileModel Model { get; set; } = TurnstileModel.ZKTeco;

        [Display(Name = "Tur")]
        public TurnstileType Type { get; set; } = TurnstileType.Tripod;

        [Display(Name = "Yo'nalish")]
        public TurnstileDirection Direction { get; set; } = TurnstileDirection.Bidirectional;

        [Display(Name = "Holat")]
        public TurnstileStatus Status { get; set; } = TurnstileStatus.Online;

        [Display(Name = "Yuzni tanish")]
        public bool FaceRecognitionEnabled { get; set; } = true;

        [Display(Name = "RFID karta")]
        public bool RfidEnabled { get; set; } = true;

        [Display(Name = "QR kod")]
        public bool QrCodeEnabled { get; set; }

        [Display(Name = "Bugungi o'tishlar")]
        public int TodayPassCount { get; set; }

        [Display(Name = "Bugungi rad")]
        public int TodayDenyCount { get; set; }

        [Display(Name = "Uptime")]
        [StringLength(20)]
        public string Uptime { get; set; } = "99.9%";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActivityTime { get; set; }

        // Navigation
        public int? LinkedCameraId { get; set; }

        [ForeignKey("LinkedCameraId")]
        public Camera? LinkedCamera { get; set; }

        public ICollection<TurnstilePermission> Permissions { get; set; } = new List<TurnstilePermission>();
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
    }

    public enum TurnstileModel
    {
        [Display(Name = "ZKTeco TS2011")]
        ZKTeco,
        [Display(Name = "Hikvision DS-K3B")]
        Hikvision,
        [Display(Name = "Dahua ASGB1100")]
        Dahua
    }

    public enum TurnstileType
    {
        [Display(Name = "Tripod (uch oyoqli)")]
        Tripod,
        [Display(Name = "Speed gate")]
        SpeedGate,
        [Display(Name = "Flap barrier")]
        FlapBarrier,
        [Display(Name = "Full height")]
        FullHeight
    }

    public enum TurnstileDirection
    {
        [Display(Name = "Ikki tomonlama")]
        Bidirectional,
        [Display(Name = "Faqat kirish")]
        EntryOnly,
        [Display(Name = "Faqat chiqish")]
        ExitOnly
    }

    public enum TurnstileStatus
    {
        [Display(Name = "Faol")]
        Online,
        [Display(Name = "Oflayn")]
        Offline,
        [Display(Name = "Bloklangan")]
        Blocked
    }
}