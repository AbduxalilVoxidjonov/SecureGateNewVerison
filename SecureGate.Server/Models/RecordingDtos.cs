using SecureGate.Domain.Cameras;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Api.Models
{
    /// <summary>
    /// Arxiv ro'yxatidagi bitta kamera.
    /// Camera entity'da RTSP credential'lari bor — ular hech qachon bu yerga tushmaydi.
    /// </summary>
    public class RecordingCameraDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CameraCode { get; set; } = string.Empty;

        /// <summary>Joylashuv — hozircha kamera guruhining nomi (alohida Location maydoni yo'q).</summary>
        public string? Location { get; set; }

        public DeviceKind DeviceKind { get; set; }
        public int? ChannelNumber { get; set; }
        public CameraStatus Status { get; set; }

        /// <summary>NVR arxivi shu kamera uchun ishlaydimi (INvrArchiveService.Supports).</summary>
        public bool ArchiveSupported { get; set; }

        public static RecordingCameraDto From(Camera camera, bool archiveSupported) => new()
        {
            Id = camera.Id,
            Name = camera.Name,
            CameraCode = camera.CameraCode,
            Location = camera.CameraGroup?.Name,
            DeviceKind = camera.DeviceKind,
            ChannelNumber = camera.ChannelNumber,
            Status = camera.Status,
            ArchiveSupported = archiveSupported
        };
    }

    /// <summary>Arxiv javobining "camera" bo'limi — qisqartirilgan shakl.</summary>
    public class RecordingCameraBriefDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CameraCode { get; set; } = string.Empty;
        public DeviceKind DeviceKind { get; set; }
        public int? ChannelNumber { get; set; }

        public static RecordingCameraBriefDto From(Camera camera) => new()
        {
            Id = camera.Id,
            Name = camera.Name,
            CameraCode = camera.CameraCode,
            DeviceKind = camera.DeviceKind,
            ChannelNumber = camera.ChannelNumber
        };
    }

    /// <summary>NVR arxividagi bitta yozuv bo'lagi.</summary>
    public class ArchiveSegmentDto
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public long DurationSeconds { get; set; }
        public long? SizeBytes { get; set; }

        public static ArchiveSegmentDto From(ArchiveSegment segment)
        {
            var start = AsUtc(segment.StartUtc);
            var end = AsUtc(segment.EndUtc);
            var seconds = (long)Math.Round((end - start).TotalSeconds);

            return new ArchiveSegmentDto
            {
                StartUtc = start,
                EndUtc = end,
                DurationSeconds = seconds < 0 ? 0 : seconds,
                SizeBytes = segment.SizeBytes
            };
        }

        public static List<ArchiveSegmentDto> FromMany(IEnumerable<ArchiveSegment>? segments) =>
            segments?.Select(From).ToList() ?? new List<ArchiveSegmentDto>();

        // JSON'da "Z" bilan chiqishi uchun Kind majburan Utc qilinadi.
        private static DateTime AsUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>GET /api/recordings/camera/{id} javobi.</summary>
    public class CameraArchiveResponseDto
    {
        public RecordingCameraBriefDto Camera { get; set; } = new();
        public bool ArchiveSupported { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<ArchiveSegmentDto> Segments { get; set; } = new();

        /// <summary>
        /// Foydalanuvchiga ko'rsatiladigan izoh (masalan arxiv qo'llab-quvvatlanmasa sababi).
        /// ApiResponse.Message klientga yetib bormaydi — client.js o'rovni tashlab
        /// faqat `data` ni qaytaradi — shuning uchun izoh payload ichida ham bo'lishi kerak.
        /// </summary>
        public string? Message { get; set; }
    }
}
