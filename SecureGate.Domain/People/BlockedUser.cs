using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.People
{
    // ==================== BLOCKED USER ====================
    public class BlockedUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Sabab")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Sabab turi")]
        public BlockReason ReasonType { get; set; }

        [Display(Name = "Bloklangan sana")]
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Bloklagan shaxs")]
        public string? BlockedBy { get; set; }

        [Display(Name = "Muddat")]
        public string? Duration { get; set; } // "3 kun", "Muddatsiz", "Karta tiklanguncha"

        [Display(Name = "Tugash sanasi")]
        public DateTime? ExpiresAt { get; set; }

        // Foydalanuvchi (biri bo'ladi)
        public int? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Users? Student { get; set; }

        public int? TeacherId { get; set; }
        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }

        public int? StaffId { get; set; }
        [ForeignKey("StaffId")]
        public Staff? Staff { get; set; }

        [NotMapped]
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    }

    public enum BlockReason
    {
        [Display(Name = "To'lov qarzdorligi")]
        PaymentDebt,
        [Display(Name = "Intizom buzilishi")]
        Discipline,
        [Display(Name = "Kartani yo'qotgan")]
        LostCard,
        [Display(Name = "Boshqa")]
        Other
    }
}
