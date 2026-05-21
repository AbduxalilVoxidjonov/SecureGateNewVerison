using System.ComponentModel.DataAnnotations;
using SecureGate.Domain;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    // ==================== CAMERA USER INDEX ====================
    public class CameraUserIndexViewModel
    {
        public List<CameraUser> Items { get; set; } = new();

        // Filtrlar
        public string? SearchTerm { get; set; }
        public int? CameraId { get; set; }
        public CameraUserType? UserType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool? ReviewedOnly { get; set; }

        // Dropdown manbalari
        public List<Camera> Cameras { get; set; } = new();

        // Statistika (joriy filtr bo'yicha)
        public int TotalCount { get; set; }
        public int TodayCount { get; set; }
        public int UnknownCount { get; set; }
        public int UniquePeopleCount { get; set; }

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
    }

    // ==================== CAMERA USER CREATE / EDIT ====================
    public class CameraUserCreateViewModel
    {
        [Required(ErrorMessage = "Ism kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Ism")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Familiya kiritish shart")]
        [StringLength(50)]
        [Display(Name = "Familiya")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Foydalanuvchi turi")]
        public CameraUserType UserType { get; set; } = CameraUserType.Unknown;

        [Required(ErrorMessage = "Kamera tanlash shart")]
        [Display(Name = "Kamera")]
        public int? CameraId { get; set; }

        [Display(Name = "Aniqlangan vaqt")]
        public DateTime DetectedAt { get; set; } = DateTime.Now;

        [Display(Name = "Aniqlik (%)")]
        [Range(0, 100)]
        public double? Confidence { get; set; }

        [Display(Name = "Rasm")]
        public IFormFile? Photo { get; set; }

        [Display(Name = "Izoh")]
        [StringLength(500)]
        public string? Note { get; set; }

        public List<Camera> AvailableCameras { get; set; } = new();
    }

    public class CameraUserEditViewModel : CameraUserCreateViewModel
    {
        public int Id { get; set; }

        public string? ExistingPhotoPath { get; set; }

        [Display(Name = "Ko'rib chiqilgan")]
        public bool IsReviewed { get; set; }
    }

    // ==================== STATISTIKA ====================
    public class CameraUserStatsViewModel
    {
        public int TotalAllTime { get; set; }
        public int TotalToday { get; set; }
        public int TotalThisWeek { get; set; }
        public int TotalThisMonth { get; set; }
        public int UnknownCount { get; set; }
        public int ReviewedCount { get; set; }

        public List<CameraDetectionStat> ByCamera { get; set; } = new();
        public List<UserTypeStat> ByUserType { get; set; } = new();
        public List<HourStat> ByHour { get; set; } = new();
        public List<TopPersonStat> TopPeople { get; set; } = new();

        // Filtr
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class CameraDetectionStat
    {
        public int CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string CameraCode { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class UserTypeStat
    {
        public CameraUserType Type { get; set; }
        public int Count { get; set; }
    }

    public class HourStat
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }

    public class TopPersonStat
    {
        public string FullName { get; set; } = string.Empty;
        public int DetectionCount { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
