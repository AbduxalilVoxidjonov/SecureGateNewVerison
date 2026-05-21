using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.Access
{
    // ==================== ACCESS LOG ====================
    public class AccessLog
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Vaqt")]
        // Mahalliy vaqt — UI'da to'g'ridan-to'g'ri ko'rsatiladi (Asia/Tashkent)
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [Display(Name = "Usul")]
        public AccessMethod Method { get; set; }

        [Display(Name = "Natija")]
        public AccessResult Result { get; set; }

        [Display(Name = "Tafsilot")]
        public string? Details { get; set; }

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

        // Turniket va kamera
        public int? TurnstileId { get; set; }
        [ForeignKey("TurnstileId")]
        public Turnstile? Turnstile { get; set; }

        public int? CameraId { get; set; }
        [ForeignKey("CameraId")]
        public Camera? Camera { get; set; }

        // Yuz tanish bo'lsa
        public double? FaceConfidence { get; set; }

        // Yuz tanish paytida olingan rasm (tanilgan yoki tanilmagan bo'lsa ham saqlanadi)
        [Display(Name = "Olingan rasm")]
        public string? CapturedImagePath { get; set; }

        [NotMapped]
        public string UserName
        {
            get
            {
                if (Student != null) return Student.FullName;
                if (Teacher != null) return Teacher.FullName;
                if (Staff != null) return Staff.FullName;
                return "NOMA'LUM";
            }
        }

        [NotMapped]
        public string UserType
        {
            get
            {
                if (StudentId.HasValue) return "O'quvchi";
                if (TeacherId.HasValue) return "O'qituvchi";
                if (StaffId.HasValue) return "Xodim";
                return "Noma'lum";
            }
        }
    }

    public enum AccessMethod
    {
        [Display(Name = "Yuz")]
        Face,
        [Display(Name = "Karta")]
        Card,
        [Display(Name = "QR kod")]
        QrCode,
        [Display(Name = "Manual")]
        Manual
    }

    public enum AccessResult
    {
        [Display(Name = "Muvaffaqiyatli")]
        Granted,
        [Display(Name = "Rad etildi")]
        Denied,
        [Display(Name = "Noma'lum")]
        Unknown
    }
}