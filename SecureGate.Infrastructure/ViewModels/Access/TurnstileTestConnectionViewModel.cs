using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Access
{
    /// <summary>
    /// Turniketni saqlashdan oldin ulanishni tekshirish uchun kiritilgan ma'lumotlar.
    /// Turniket — TCP qurilma (masalan ZKTeco 4370-portda), shu sababli IP/port bo'yicha tekshiriladi.
    /// </summary>
    public class TurnstileTestConnectionViewModel
    {
        [StringLength(100)]
        public string? IpAddress { get; set; }

        [Range(1, 65535, ErrorMessage = "Port 1 dan 65535 gacha bo'lishi kerak")]
        public int Port { get; set; } = 4370;
    }
}
