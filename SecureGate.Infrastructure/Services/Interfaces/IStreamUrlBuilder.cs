namespace SecureGate.Infrastructure.Services.Interfaces;

/// <summary>Qaysi oqim kerakligi: main (sifatli, ko'rsatish/yozish) yoki sub (yengil, AI uchun).</summary>
public enum StreamPurpose
{
    /// <summary>Main stream — FullHD/4K. Ko'rsatish va yozib olish uchun.</summary>
    Main,

    /// <summary>Sub-stream — 480p/720p. Yuz tanish (AI) uchun, CPU/GPU tejaydi.</summary>
    Sub
}

/// <summary>
/// URL qurish uchun kerak bo'lgan xom ma'lumotlar. <see cref="Camera"/> entity'siga bog'liq emas —
/// shu sababli hali DB'ga saqlanmagan "test ulanish" formasi uchun ham ishlatiladi.
/// <para><b>DIQQAT:</b> <see cref="Password"/> bu yerda OCHIQ MATN (plain-text) bo'lishi shart.
/// <see cref="Camera"/> dan qurilganda <c>ICameraCredentialProtector.Unprotect</c> orqali ochiladi.</para>
/// </summary>
public sealed record StreamEndpoint
{
    /// <summary>Foydalanuvchi kiritgan to'liq main-stream URL (bo'lsa — o'zgartirilmaydi).</summary>
    public string? StreamUrl { get; init; }

    /// <summary>Foydalanuvchi kiritgan to'liq sub-stream URL (bo'lsa — o'zgartirilmaydi).</summary>
    public string? AiStreamUrl { get; init; }

    /// <summary>Kamera yoki NVR ning IP manzili / hostname'i.</summary>
    public string? IpAddress { get; init; }

    /// <summary>RTSP porti (odatda 554).</summary>
    public int Port { get; init; } = 554;

    public string? Username { get; init; }

    /// <summary>OCHIQ MATN parol.</summary>
    public string? Password { get; init; }

    /// <summary>Vendor — shablon tanlash uchun.</summary>
    public CameraModel CameraModel { get; init; } = CameraModel.Hikvision;

    /// <summary>NVR kanal raqami (1 dan boshlab). null bo'lsa 1 deb olinadi.</summary>
    public int? ChannelNumber { get; init; }
}

/// <summary>
/// Vendorga bog'liq RTSP URL qurish qatlami. Ilgari bu mantiq
/// <c>CameraStreamWorker</c>, <c>CameraMjpegStreamer</c> va <c>DeviceConnectionTester</c> da
/// uch marta takrorlangan va Hikvision'ga hardcode qilingan edi.
/// <para>Implementatsiya holatsiz (stateless) — <b>Singleton</b> sifatida ro'yxatga olinadi.</para>
/// </summary>
public interface IStreamUrlBuilder
{
    /// <summary>Kirish ma'lumotlari joylashtirilgan to'liq RTSP URL. Qura olmasa null.</summary>
    string? BuildLive(Camera camera, StreamPurpose purpose);

    /// <summary>
    /// <see cref="BuildLive(Camera, StreamPurpose)"/> ning entity'siz varianti —
    /// hali saqlanmagan forma ma'lumotlari uchun (parol ochiq matnda keladi).
    /// </summary>
    string? BuildLive(StreamEndpoint endpoint, StreamPurpose purpose);

    /// <summary>
    /// Host/port/kirish ma'lumotlarini qo'shib, berilgan yo'l bilan RTSP URL yasaydi:
    /// <c>rtsp://{user}:{pass}@{host}:{port}{pathAndQuery}</c>.
    /// Vendorga xos yo'lni (masalan Hikvision arxiv <c>/Streaming/tracks/...</c>) chaqiruvchi beradi —
    /// bu yerda faqat kirish ma'lumotlari va manzil bir joyda yig'iladi.
    /// IP manzil bo'sh bo'lsa null.
    /// </summary>
    string? BuildWithPath(Camera camera, string pathAndQuery);

    /// <summary>URL'dagi login/parolni <c>***:***</c> bilan yashiradi — log yozish uchun.</summary>
    string Mask(string? url);
}
