using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    public class CameraGroupListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CameraCount { get; set; }
        public List<string> CameraNames { get; set; } = new();
    }

    public class CameraGroupFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Guruh nomi majburiy")]
        [StringLength(50, ErrorMessage = "Guruh nomi 50 belgidan oshmasligi kerak")]
        [Display(Name = "Guruh nomi")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Kameralar")]
        public List<int> SelectedCameraIds { get; set; } = new();

        // Forma uchun: barcha kameralar (id, nom, joriy guruh nomi)
        public List<CameraCheckboxItem> AvailableCameras { get; set; } = new();
    }

    public class CameraCheckboxItem
    {
        public int Id { get; set; }
        public string CameraCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CurrentGroupName { get; set; }
        public bool IsInOtherGroup { get; set; }
    }
}
