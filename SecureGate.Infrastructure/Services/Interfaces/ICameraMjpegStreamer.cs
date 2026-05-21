namespace SecureGate.Infrastructure.Services.Interfaces
{
    /// <summary>
    /// Kamera RTSP/HTTP oqimini brauzerga ko'rsatish uchun.
    /// Stream — uzluksiz MJPEG (multipart), Snapshot — bitta JPEG kadr.
    /// </summary>
    public interface ICameraMjpegStreamer
    {
        /// <summary>
        /// Kameradan uzluksiz MJPEG oqimni <paramref name="output"/> ga yozadi
        /// (multipart/x-mixed-replace; boundary=frame). Mijoz uzilganda to'xtaydi.
        /// </summary>
        Task StreamAsync(Camera camera, Stream output, int? maxWidth, CancellationToken ct);

        /// <summary>Kameradan bitta JPEG kadr oladi. Ulanib bo'lmasa null qaytaradi.</summary>
        Task<byte[]?> SnapshotAsync(Camera camera, int? maxWidth, CancellationToken ct);
    }
}
