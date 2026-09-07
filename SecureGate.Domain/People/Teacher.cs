using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.People
{
    public class Teacher
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "F.I.O kiritish shart")]
        [StringLength(100)]
        [Display(Name = "F.I.O")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Fan")]
        public string Subject { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Tajriba (yil)")]
        public int ExperienceYears { get; set; }

        [Display(Name = "Lavozim")]
        public TeacherPosition Position { get; set; } = TeacherPosition.Teacher;

        [Display(Name = "Holat")]
        public TeacherStatus Status { get; set; } = TeacherStatus.Active;

        [Display(Name = "Rasm")]
        [StringLength(500)]
        public string? PhotoPath { get; set; }

        public bool FaceRecognitionEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "So'nggi kirish")]
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

    public enum TeacherPosition
    {
        [Display(Name = "O'qituvchi")]
        Teacher,
        [Display(Name = "Bosh o'qituvchi")]
        HeadTeacher,
        [Display(Name = "Metodist")]
        Methodist
    }

    public enum TeacherStatus
    {
        [Display(Name = "Faol")]
        Active,
        [Display(Name = "Ta'tilda")]
        OnLeave,
        [Display(Name = "Ishdan bo'shatilgan")]
        Dismissed
    }
}