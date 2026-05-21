using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Cameras
{
    public class CameraGroup
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Guruh nomi")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Camera> Cameras { get; set; } = new List<Camera>();

        [NotMapped]
        public int CameraCount => Cameras?.Count ?? 0;
    }

    public enum CameraProtocol
    {
        RTSP,
        ONVIF,
        [Display(Name = "HTTP / MJPEG")]
        HTTP,
        RTMP
    }

    public enum CameraModel
    {
        Hikvision,
        Dahua,
        Axis,
        [Display(Name = "Boshqa")]
        Other
    }

    public enum VideoQuality
    {
        [Display(Name = "4K (3840×2160)")]
        UHD4K,
        [Display(Name = "Full HD (1920×1080)")]
        FullHD,
        [Display(Name = "HD (1280×720)")]
        HD
    }

    public enum CameraStatus
    {
        [Display(Name = "Faol")]
        Online,
        [Display(Name = "Oflayn")]
        Offline,
        [Display(Name = "Yozib olinyapti")]
        Recording
    }

    public enum CameraType
    {
        // Turniketga bog'langan: yuz tanildi → AccessLog + turniket avtomatik ochiladi
        [Display(Name = "Turniket kamerasi")]
        Turnstile,

        // Faqat kuzatuv: yuz tanildi → CameraUser jadvaliga yoziladi, turniket ochilmaydi
        [Display(Name = "Oddiy kamera")]
        Regular
    }
}
