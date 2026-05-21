using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SecureGate.Domain.People
{
    public class Users
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ism kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Ism")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Familiya kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Familiya")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "ID raqam")]
        public string StudentId { get; set; } = string.Empty;

        [Display(Name = "Tug'ilgan sana")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Jinsi")]
        public Gender Gender { get; set; }

        [Phone]
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Phone]
        [Display(Name = "Ota-ona telefoni")]
        public string? ParentPhone { get; set; }

        [Display(Name = "Manzil")]
        public string? Address { get; set; }

        [Display(Name = "Rasm")]
        public string? PhotoPath { get; set; }

        [Display(Name = "Holat")]
        public StudentStatus Status { get; set; } = StudentStatus.Active;

        [Display(Name = "Yuz tanish")]
        public bool FaceRecognitionEnabled { get; set; } = true;

        [Display(Name = "SMS bildirishnoma")]
        public bool SmsNotification { get; set; }

        [Display(Name = "Qo'shilgan sana")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation

        public ICollection<TurnstilePermission> TurnstilePermissions { get; set; } = new List<TurnstilePermission>();
        public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
        public ICollection<FaceData> FaceDataList { get; set; } = new List<FaceData>();
        public BlockedUser? BlockedUser { get; set; }

        // Computed
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";

        [NotMapped]
        public string Initials => $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}";

        [NotMapped]
        public bool IsBlocked => Status == StudentStatus.Blocked;
    }

    public enum Gender
    {
        [Display(Name = "Erkak")]
        Male,
        [Display(Name = "Ayol")]
        Female
    }

    public enum StudentStatus
    {
        [Display(Name = "Faol")]
        Active,
        [Display(Name = "Bloklangan")]
        Blocked,
        [Display(Name = "Yangi")]
        New,
        [Display(Name = "Arxivlangan")]
        Archived
    }
}