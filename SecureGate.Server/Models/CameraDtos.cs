using SecureGate.Domain.Cameras;

namespace SecureGate.Api.Models
{
    /// <summary>
    /// Kamera uchun tashqariga chiqadigan xavfsiz shakl.
    /// Camera entity'sida RTSP credential'lari bor (Username/Password/StreamUrl/AiStreamUrl) —
    /// ular HECH QACHON API javobiga tushmasligi kerak.
    /// </summary>
    public class CameraResponseDto
    {
        public int Id { get; set; }
        public string CameraCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public CameraType Type { get; set; }
        public CameraProtocol Protocol { get; set; }
        public CameraModel CameraModel { get; set; }
        public VideoQuality Quality { get; set; }
        public CameraStatus Status { get; set; }

        /// <summary>Quality'ning o'qiladigan ko'rinishi (masalan "Full HD (1920×1080)").</summary>
        public string Resolution { get; set; } = string.Empty;

        public int Fps { get; set; }
        public bool FaceRecognitionEnabled { get; set; }
        public bool ContinuousRecording { get; set; }
        public bool MotionDetection { get; set; }

        public string? IpAddress { get; set; }
        public int Port { get; set; }

        /// <summary>
        /// Qurilma turi: Camera (to'g'ridan-to'g'ri IP-kamera) yoki NvrChannel (NVR kanali).
        /// NvrChannel bo'lsa IpAddress/Port — NVR manzili.
        /// </summary>
        public DeviceKind DeviceKind { get; set; }

        /// <summary>NVR kanal raqami (1 dan boshlab). DeviceKind == NvrChannel bo'lgandagina to'ldiriladi.</summary>
        public int? ChannelNumber { get; set; }

        public int? CameraGroupId { get; set; }
        public string? GroupName { get; set; }

        public DateTime CreatedAt { get; set; }
        public string StatusMeta { get; set; } = string.Empty;

        /// <summary>Kamera uchun login/parol saqlanganmi (qiymatning o'zi qaytmaydi).</summary>
        public bool HasCredentials { get; set; }

        public static CameraResponseDto From(Camera camera) => new()
        {
            Id = camera.Id,
            CameraCode = camera.CameraCode,
            Name = camera.Name,
            Type = camera.Type,
            Protocol = camera.Protocol,
            CameraModel = camera.CameraModel,
            Quality = camera.Quality,
            Status = camera.Status,
            Resolution = camera.Quality.GetDisplayName(),
            Fps = camera.Fps,
            FaceRecognitionEnabled = camera.FaceRecognitionEnabled,
            ContinuousRecording = camera.ContinuousRecording,
            MotionDetection = camera.MotionDetection,
            IpAddress = camera.IpAddress,
            Port = camera.Port,
            DeviceKind = camera.DeviceKind,
            ChannelNumber = camera.ChannelNumber,
            CameraGroupId = camera.CameraGroupId,
            GroupName = camera.CameraGroup?.Name,
            CreatedAt = camera.CreatedAt,
            StatusMeta = camera.StatusMeta,
            HasCredentials = !string.IsNullOrEmpty(camera.Username) || !string.IsNullOrEmpty(camera.Password)
        };

        public static CameraResponseDto? FromOrNull(Camera? camera) =>
            camera is null ? null : From(camera);

        public static List<CameraResponseDto> FromMany(IEnumerable<Camera>? cameras) =>
            cameras?.Select(From).ToList() ?? new List<CameraResponseDto>();
    }

    /// <summary>
    /// Kamera guruhi — ichidagi kameralar ham DTO ko'rinishida.
    /// </summary>
    public class CameraGroupResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CameraCount { get; set; }
        public List<CameraResponseDto> Cameras { get; set; } = new();

        public static CameraGroupResponseDto From(CameraGroup group) => new()
        {
            Id = group.Id,
            Name = group.Name,
            CameraCount = group.Cameras?.Count ?? 0,
            Cameras = CameraResponseDto.FromMany(group.Cameras)
        };

        public static List<CameraGroupResponseDto> FromMany(IEnumerable<CameraGroup>? groups) =>
            groups?.Select(From).ToList() ?? new List<CameraGroupResponseDto>();
    }

    /// <summary>
    /// Camera entity boshqa entity'ning navigation'i sifatida qaytadigan joylar uchun
    /// (CameraUser.Camera, Turnstile.LinkedCamera) — maxfiy maydonlarni o'chirib tashlaydi.
    /// Faqat GET (SaveChanges chaqirilmaydigan) action'larda ishlatiladi.
    /// </summary>
    public static class CameraSecrets
    {
        public static Camera? Scrub(Camera? camera)
        {
            if (camera is null) return null;
            camera.Username = null;
            camera.Password = null;
            camera.StreamUrl = null;
            camera.AiStreamUrl = null;
            return camera;
        }

        public static void ScrubAll(IEnumerable<Camera?>? cameras)
        {
            if (cameras is null) return;
            foreach (var camera in cameras) Scrub(camera);
        }
    }
}
