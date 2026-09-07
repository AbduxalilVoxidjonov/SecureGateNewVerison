using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.People
{
    public class Staff
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "F.I.O")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Lavozim")]
        public string Position { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Bo'lim")]
        public Department Department { get; set; }

        [Display(Name = "Smena")]
        public ShiftType Shift { get; set; } = ShiftType.Day;

        [Phone]
        [StringLength(20)]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Display(Name = "Kirish darajasi")]
        public AccessLevel AccessLevel { get; set; } = AccessLevel.Standard;

        [Display(Name = "Holat")]
        public StaffStatus Status { get; set; } = StaffStatus.Active;

        [Display(Name = "Rasm")]
        [StringLength(500)]
        public string? PhotoPath { get; set; }

        public bool FaceRecognitionEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastAccessTime { get; set; }

        // Navigation
        public ICollection<TurnstilePermission> TurnstilePermissions { get; set; } = new List<TurnstilePermission>();
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();

        [NotMapped]
        public string Initials
        {
            get
            {
                var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}";
                return parts.Length > 0 ? parts[0][0].ToString() : "?";
            }
        }
    }

    public enum Department
    {
        [Display(Name = "Direksiya")]
        Administration,
        [Display(Name = "Hisobxona")]
        Accounting,
        [Display(Name = "Texnik xizmat")]
        Technical,
        [Display(Name = "Oshxona")]
        Kitchen,
        [Display(Name = "Qo'riqlash")]
        Security,
        [Display(Name = "Tibbiyot")]
        Medical
    }

    public enum ShiftType
    {
        [Display(Name = "Kunduzgi (08:00-17:00)")]
        Day,
        [Display(Name = "Tungi (20:00-08:00)")]
        Night,
        [Display(Name = "24/7")]
        FullTime
    }

    public enum AccessLevel
    {
        [Display(Name = "Standart")]
        Standard,
        [Display(Name = "Yuqori")]
        High,
        [Display(Name = "To'liq")]
        Full
    }

    public enum StaffStatus
    {
        [Display(Name = "Faol")]
        Active,
        [Display(Name = "Ta'tilda")]
        OnLeave,
        [Display(Name = "Ishdan bo'shatilgan")]
        Dismissed
    }
}