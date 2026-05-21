namespace SecureGate.Infrastructure.Services.Interfaces
{
    // CameraStreamWorker yuz aniqlaganida (tanilgan yoki tanilmagan) shu handler
    // chaqiriladi. U: snapshot saqlaydi, AccessLog yozadi, turniketni ochadi,
    // SignalR orqali UI'ga xabar beradi.
    public interface IFaceMatchHandler
    {
        Task HandleAsync(FaceMatchEvent ev, CancellationToken ct = default);
    }

    public sealed record FaceMatchEvent(
        int CameraId,
        string PersonType,   // "Student" | "Teacher" | "Staff" | "Unknown"
        int? PersonId,
        string FullName,
        float Confidence,    // 0..1 (kosinus o'xshashlik)
        byte[]? SnapshotJpeg,
        BoundingBox Box,
        int FrameWidth,
        int FrameHeight);
}
