using System.Globalization;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations;

/// <summary>
/// <see cref="IStreamUrlBuilder"/> ning yagona implementatsiyasi.
///
/// <para>Ustuvorlik tartibi (mavjud xatti-harakat saqlangan):</para>
/// <list type="bullet">
///   <item><b>Sub</b> (yuz tanish): AiStreamUrl → StreamUrl → vendor shabloni</item>
///   <item><b>Main</b> (ko'rsatish/yozish): StreamUrl → AiStreamUrl → vendor shabloni</item>
/// </list>
///
/// <para>
/// Foydalanuvchi to'liq URL kiritgan bo'lsa u <b>o'zgartirilmaydi</b> — faqat URL'da
/// login qismi bo'lmasa va DB'da login bo'lsa, kirish ma'lumotlari qo'shiladi
/// (bu ilgari ham shunday ishlagan — NVR foydalanuvchilari uchun yagona ishlaydigan yo'l).
/// </para>
///
/// <para>Holatsiz — <b>Singleton</b>.</para>
/// </summary>
public sealed class StreamUrlBuilder : IStreamUrlBuilder
{
    private readonly ICameraCredentialProtector _cred;

    public StreamUrlBuilder(ICameraCredentialProtector cred) => _cred = cred;

    // ===== Public API =====

    public string? BuildLive(Camera camera, StreamPurpose purpose)
    {
        if (camera is null) return null;
        return BuildLive(ToEndpoint(camera), purpose);
    }

    public string? BuildLive(StreamEndpoint endpoint, StreamPurpose purpose)
    {
        if (endpoint is null) return null;

        // 1) Foydalanuvchi kiritgan URL'lar — vazifaga qarab ustuvorlik.
        var preferred = purpose == StreamPurpose.Sub ? endpoint.AiStreamUrl : endpoint.StreamUrl;
        var fallback = purpose == StreamPurpose.Sub ? endpoint.StreamUrl : endpoint.AiStreamUrl;

        if (IsStreamScheme(preferred))
            return InjectCredentials(preferred!.Trim(), endpoint.Username, endpoint.Password);

        if (IsStreamScheme(fallback))
            return InjectCredentials(fallback!.Trim(), endpoint.Username, endpoint.Password);

        // 2) Ikkalasi ham yo'q — vendor shabloni bo'yicha yasaymiz.
        return BuildFromTemplate(endpoint, purpose);
    }

    public string? BuildWithPath(Camera camera, string pathAndQuery)
    {
        if (camera is null) return null;

        var host = camera.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(host)) return null;

        var port = camera.Port is > 0 and <= 65535 ? camera.Port : 554;
        var creds = BuildCredentialsPrefix(camera.Username, _cred.Unprotect(camera.Password));
        var path = string.IsNullOrEmpty(pathAndQuery) ? "/" : pathAndQuery;
        if (path[0] != '/') path = "/" + path;

        return $"rtsp://{creds}{host}:{port.ToString(CultureInfo.InvariantCulture)}{path}";
    }

    public string Mask(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        try
        {
            var uri = new Uri(url);
            if (string.IsNullOrEmpty(uri.UserInfo)) return url;
            return $"{uri.Scheme}://***:***@{uri.Authority}{uri.PathAndQuery}";
        }
        catch
        {
            return url;
        }
    }

    // ===== Camera -> StreamEndpoint =====

    /// <summary>Parolni deshifrlab, xom endpoint tavsifiga aylantiradi.</summary>
    private StreamEndpoint ToEndpoint(Camera cam) => new()
    {
        StreamUrl = cam.StreamUrl,
        AiStreamUrl = cam.AiStreamUrl,
        IpAddress = cam.IpAddress,
        Port = cam.Port,
        Username = cam.Username,
        Password = _cred.Unprotect(cam.Password),
        CameraModel = cam.CameraModel,
        ChannelNumber = cam.ChannelNumber
    };

    // ===== Vendor shablonlari =====

    private static string? BuildFromTemplate(StreamEndpoint ep, StreamPurpose purpose)
    {
        var host = ep.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(host)) return null;

        var port = ep.Port is > 0 and <= 65535 ? ep.Port : 554;
        var channel = ep.ChannelNumber is > 0 ? ep.ChannelNumber.Value : 1;
        var creds = BuildCredentialsPrefix(ep.Username, ep.Password);
        var authority = $"{creds}{host}:{port.ToString(CultureInfo.InvariantCulture)}";

        return ep.CameraModel switch
        {
            // Dahua: /cam/realmonitor?channel=1&subtype=0 (main) / subtype=1 (sub)
            CameraModel.Dahua =>
                $"rtsp://{authority}/cam/realmonitor?channel={channel}&subtype={(purpose == StreamPurpose.Sub ? 1 : 0)}",

            // Axis: /axis-media/media.amp?camera=1 (sub/main farqi resolution parametri bilan beriladi,
            // vendor standart yo'lida alohida kanal yo'q).
            CameraModel.Axis =>
                $"rtsp://{authority}/axis-media/media.amp?camera={channel}",

            // Hikvision (va noma'lum vendorlar uchun default):
            // /Streaming/Channels/{ch}01 (main) / {ch}02 (sub). Kanal 1 -> 101/102, kanal 3 -> 301/302.
            _ =>
                $"rtsp://{authority}/Streaming/Channels/{channel}0{(purpose == StreamPurpose.Sub ? 2 : 1)}"
        };
    }

    // ===== Yordamchilar =====

    /// <summary>
    /// <c>user:pass@</c> prefiksi. Login bo'sh bo'lsa — bo'sh satr (creds qismi umuman qo'shilmaydi).
    /// Parol <see cref="Uri.EscapeDataString"/> bilan kodlanadi (mavjud naqsh — parolda @ / : bo'lishi mumkin).
    /// </summary>
    private static string BuildCredentialsPrefix(string? username, string? plainPassword)
    {
        if (string.IsNullOrEmpty(username)) return string.Empty;
        return $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(plainPassword ?? string.Empty)}@";
    }

    /// <summary>Qo'llab-quvvatlanadigan sxema: rtsp / rtmp / http(s).</summary>
    private static bool IsStreamScheme(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
         || url.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase)
         || url.StartsWith("http", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// URL'da login qismi bo'lmasa — DB'dagi login/parolni qo'shadi.
    /// URL'ning yo'l (path) qismi hech qachon o'zgartirilmaydi.
    /// </summary>
    private static string InjectCredentials(string streamUrl, string? username, string? plainPassword)
    {
        try
        {
            var uri = new Uri(streamUrl);
            if (!string.IsNullOrEmpty(uri.UserInfo)) return streamUrl; // login allaqachon URL ichida
            if (string.IsNullOrEmpty(username)) return streamUrl;

            var creds = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(plainPassword ?? string.Empty)}";
            return $"{uri.Scheme}://{creds}@{uri.Authority}{uri.PathAndQuery}";
        }
        catch
        {
            // URI parse bo'lmasa — foydalanuvchi kiritganini o'zgartirmasdan qaytaramiz.
            return streamUrl;
        }
    }
}
