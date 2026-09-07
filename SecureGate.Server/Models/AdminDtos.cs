using SecureGate.Domain.Auth;

namespace SecureGate.Api.Models
{
    /// <summary>
    /// AdminEditViewModel'ning javob varianti: AvailableCameraGroups ichidagi
    /// kameralar entity emas, DTO ko'rinishida (RTSP parollari chiqib ketmasligi uchun).
    /// JSON maydon nomlari eski shakl bilan bir xil.
    /// </summary>
    public class AdminEditResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public List<Permission> SelectedPermissions { get; set; } = new();
        public List<int> SelectedCameraGroupIds { get; set; } = new();
        public List<CameraGroupResponseDto> AvailableCameraGroups { get; set; } = new();
    }
}
