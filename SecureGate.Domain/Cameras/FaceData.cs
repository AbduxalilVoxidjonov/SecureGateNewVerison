using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Cameras
{
    // ==================== FACE DATA ====================
    public class FaceData
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Rasm yo'li")]
        public string ImagePath { get; set; } = string.Empty;

        [Display(Name = "Encoding")]
        public string? FaceEncoding { get; set; } // Base64 yoki vektor

        [Display(Name = "Aniqlik darajasi")]
        public FaceConfidenceLevel ConfidenceLevel { get; set; } = FaceConfidenceLevel.High;

        [Display(Name = "Faol")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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
        public string OwnerName
        {
            get
            {
                if (Student != null) return Student.FullName;
                if (Teacher != null) return Teacher.FullName;
                if (Staff != null) return Staff.FullName;
                return "Noma'lum";
            }
        }
    }

    public enum FaceConfidenceLevel
    {
        [Display(Name = "Yuqori (95%+)")]
        High,
        [Display(Name = "O'rta (85%+)")]
        Medium,
        [Display(Name = "Past (75%+)")]
        Low
    }
}
