using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    /// <summary>
    /// Kamerani saqlashdan oldin ulanishni tekshirish uchun kiritilgan ma'lumotlar.
    /// Parol ochiq (plain-text) keladi — hali shifrlanmagan, faqat test uchun ishlatiladi.
    /// </summary>
    public class CameraTestConnectionViewModel
    {
        [StringLength(500)]
        public string? StreamUrl { get; set; }

        [StringLength(500)]
        public string? AiStreamUrl { get; set; }

        [StringLength(100)]
        public string? IpAddress { get; set; }

        [Range(1, 65535, ErrorMessage = "Port 1 dan 65535 gacha bo'lishi kerak")]
        public int Port { get; set; } = 554;

        [StringLength(50)]
        public string? Username { get; set; }

        [StringLength(200)]
        public string? Password { get; set; }
    }
}
