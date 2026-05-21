namespace SecureGate.Infrastructure.Services.Interfaces
{
    /// <summary>
    /// Kamera/turniketni qo'shishdan oldin "test ulanib ko'rish" xizmati.
    /// DB'ga hech narsa yozmaydi — faqat tarmoq/oqim ulanishini tekshiradi.
    /// </summary>
    public interface IDeviceConnectionTester
    {
        /// <summary>Kamera oqimini (RTSP/HTTP) haqiqatdan ochib, bitta kadr olishga urinadi.</summary>
        Task<ConnectionTestResult> TestCameraAsync(CameraTestConnectionViewModel model, CancellationToken ct = default);

        /// <summary>Berilgan host:port ga TCP ulanish mumkinligini tekshiradi (turniket va h.k. uchun).</summary>
        Task<ConnectionTestResult> TestTcpAsync(string? host, int port, CancellationToken ct = default);
    }
}
