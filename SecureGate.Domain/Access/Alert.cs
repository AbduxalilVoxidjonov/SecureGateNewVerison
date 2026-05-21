using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Access
{

    // ==================== ALERT ====================
    public class Alert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Sarlavha")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Tafsilot")]
        public string? Message { get; set; }

        [Display(Name = "Tur")]
        public AlertType Type { get; set; }

        [Display(Name = "Vaqt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "O'qilgan")]
        public bool IsRead { get; set; }

        public int? CameraId { get; set; }
        [ForeignKey("CameraId")]
        public Camera? Camera { get; set; }

        public int? TurnstileId { get; set; }
        [ForeignKey("TurnstileId")]
        public Turnstile? Turnstile { get; set; }
    }

    public enum AlertType
    {
        [Display(Name = "Ma'lumot")]
        Info,
        [Display(Name = "Ogohlantirish")]
        Warning,
        [Display(Name = "Xavfli")]
        Danger,
        [Display(Name = "Muvaffaqiyat")]
        Success
    }
}
