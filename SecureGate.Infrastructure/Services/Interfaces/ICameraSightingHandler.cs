namespace SecureGate.Infrastructure.Services.Interfaces
{
    // Oddiy (Regular) kamera tomonidan aniqlangan yuz uchun handler.
    // CameraUser jadvaliga yozadi — turniket bilan hech qanday aloqasi yo'q.
    // FaceMatchHandler turniket kameralari uchun ishlatiladi.
    public interface ICameraSightingHandler
    {
        Task HandleAsync(FaceMatchEvent ev, CancellationToken ct = default);
    }
}
