using SecureGate.Domain;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Cameras
{
    // ==================== CAMERA ====================
    public class CameraGridViewModel
    {
        public List<Camera> Cameras { get; set; } = new();
        public List<CameraGroup> CameraGroups { get; set; } = new();
        public int? SelectedGroupId { get; set; }
        public CameraStatus? StatusFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int GridColumns { get; set; } = 3;
    }

    
}
