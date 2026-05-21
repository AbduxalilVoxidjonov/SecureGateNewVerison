using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Access
{
    // ==================== TURNSTILE PERMISSION ====================
    public class TurnstilePermission
    {
        [Key]
        public int Id { get; set; }

        public int TurnstileId { get; set; }
        [ForeignKey("TurnstileId")]
        public Turnstile Turnstile { get; set; } = null!;

        // Foydalanuvchi turi (biri bo'ladi)
        public int? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Users? Student { get; set; }

        public int? TeacherId { get; set; }
        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }

        public int? StaffId { get; set; }
        [ForeignKey("StaffId")]
        public Staff? Staff { get; set; }

        public bool IsAllowed { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
