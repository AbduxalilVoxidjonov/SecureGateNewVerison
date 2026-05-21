using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Cameras
{
    // ==================== CAMERA USER ====================
    // Kamera orqali aniqlangan foydalanuvchining yozuvi
    // (har bir kameraga tushgan inson â€” kim, qachon, qaysi kamerada)
    public class CameraUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Ism")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Familiya")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Foydalanuvchi turi")]
        public CameraUserType UserType { get; set; } = CameraUserType.Unknown;

        [Display(Name = "Aniqlangan vaqt")]
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Aniqlik (%)")]
        public double? Confidence { get; set; }

        [Display(Name = "Olingan rasm")]
        public string? CapturedImagePath { get; set; }

        [Display(Name = "Izoh")]
        [StringLength(500)]
        public string? Note { get; set; }

        [Display(Name = "Ko'rib chiqilgan")]
        public bool IsReviewed { get; set; }

        // Foydalanuvchi linki (ixtiyoriy â€” agar tanilgan bo'lsa)
        public int? StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Users? Student { get; set; }

        public int? TeacherId { get; set; }
        [ForeignKey("TeacherId")]
        public Teacher? Teacher { get; set; }

        public int? StaffId { get; set; }
        [ForeignKey("StaffId")]
        public Staff? Staff { get; set; }

        // Kamera
        public int? CameraId { get; set; }
        [ForeignKey("CameraId")]
        public Camera? Camera { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [NotMapped]
        public string Initials =>
            $"{(FirstName.Length > 0 ? FirstName[0] : '?')}{(LastName.Length > 0 ? LastName[0] : ' ')}";
    }

    public enum CameraUserType
    {
        [Display(Name = "Noma'lum")]
        Unknown,
        [Display(Name = "O'quvchi")]
        Student,
        [Display(Name = "O'qituvchi")]
        Teacher,
        [Display(Name = "Xodim")]
        Staff,
        [Display(Name = "Mehmon")]
        Guest
    }
}
