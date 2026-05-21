using System.Diagnostics;
using System.Net.Sockets;
using OpenCvSharp;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // Kamera/turniketni qo'shishdan oldin ulanishni sinab ko'rish uchun.
    // Hech narsani DB'ga yozmaydi. Kamera uchun:
    //   1) URL/IP'dan host:port aniqlanadi va TCP yetib borish tekshiriladi (tez, aniq);
    //   2) port ochiq bo'lsa — VideoCapture orqali oqim ochilib, bitta kadr olinadi.
    // Turniket uchun — faqat TCP yetib borish tekshiriladi (qurilma TCP port'da turadi).
    public class DeviceConnectionTester : IDeviceConnectionTester
    {
        private static readonly TimeSpan TcpTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan StreamTimeout = TimeSpan.FromSeconds(10);

        private readonly ILogger<DeviceConnectionTester> _logger;

        public DeviceConnectionTester(ILogger<DeviceConnectionTester> logger)
        {
            _logger = logger;
        }

        public async Task<ConnectionTestResult> TestCameraAsync(CameraTestConnectionViewModel model, CancellationToken ct = default)
        {
            var url = BuildStreamUrl(model);
            if (string.IsNullOrWhiteSpace(url))
                return ConnectionTestResult.Fail("Stream URL yoki IP manzil kiriting.");

            if (!TryParseHostPort(url, out var host, out var port))
                return ConnectionTestResult.Fail("Manzilni o'qib bo'lmadi. Stream URL yoki IP/portni tekshiring.");

            var sw = Stopwatch.StartNew();

            // 1) Tarmoq darajasida yetib borish — tez va aniq xabar beradi
            var tcp = await TryTcpAsync(host, port, ct);
            if (!tcp.Ok)
            {
                sw.Stop();
                return ConnectionTestResult.Fail(
                    $"{host}:{port} ga ulanib bo'lmadi — {tcp.Detail}. IP/port va tarmoqni tekshiring.",
                    sw.ElapsedMilliseconds);
            }

            // 2) Video oqimni haqiqatdan ochib, bitta kadr olishga urinish
            var stream = await TryOpenStreamAsync(url, ct);
            sw.Stop();

            if (stream.Ok)
                return ConnectionTestResult.Success(
                    $"Ulanish muvaffaqiyatli — {stream.Width}×{stream.Height} kadr olindi.",
                    sw.ElapsedMilliseconds, stream.Width, stream.Height);

            return ConnectionTestResult.Fail(
                $"Port ochiq ({host}:{port}), lekin video oqim ochilmadi. Login/parol yoki Stream URL noto'g'ri bo'lishi mumkin.",
                sw.ElapsedMilliseconds);
        }

        public async Task<ConnectionTestResult> TestTcpAsync(string? host, int port, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(host))
                return ConnectionTestResult.Fail("IP manzil kiriting.");
            if (port is < 1 or > 65535)
                return ConnectionTestResult.Fail("Port 1 dan 65535 gacha bo'lishi kerak.");

            var sw = Stopwatch.StartNew();
            var tcp = await TryTcpAsync(host.Trim(), port, ct);
            sw.Stop();

            return tcp.Ok
                ? ConnectionTestResult.Success($"Ulanish muvaffaqiyatli — {host}:{port} javob berdi.", sw.ElapsedMilliseconds)
                : ConnectionTestResult.Fail($"{host}:{port} ga ulanib bo'lmadi — {tcp.Detail}. IP/port va tarmoqni tekshiring.", sw.ElapsedMilliseconds);
        }

        // ===== TCP yetib borishni tekshirish =====
        private async Task<(bool Ok, string Detail)> TryTcpAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                using var client = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TcpTimeout);
                await client.ConnectAsync(host, port, timeoutCts.Token);
                return (client.Connected, "ulandi");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return (false, "vaqt tugadi (timeout)");
            }
            catch (SocketException ex)
            {
                return (false, ex.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "ulanish rad etildi",
                    SocketError.HostNotFound => "host topilmadi",
                    SocketError.TimedOut => "vaqt tugadi (timeout)",
                    SocketError.NetworkUnreachable => "tarmoq mavjud emas",
                    _ => ex.SocketErrorCode.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TCP probe xatosi {Host}:{Port}", host, port);
                return (false, "noma'lum xato");
            }
        }

        // ===== Video oqimni ochib, kadr olishga urinish =====
        private async Task<(bool Ok, int Width, int Height)> TryOpenStreamAsync(string url, CancellationToken ct)
        {
            // FFMPEG cheksiz kutib qolmasligi uchun TCP transport + socket timeout (5s, mikrosekundda).
            Environment.SetEnvironmentVariable(
                "OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp|stimeout;5000000");

            // VideoCapture bloklovchi chaqiruv — Task.Run + umumiy timeout bilan o'raymiz,
            // shunda so'rov hech qachon osilib qolmaydi.
            var task = Task.Run(() =>
            {
                VideoCapture? cap = null;
                try
                {
                    cap = new VideoCapture(url);
                    cap.Set(VideoCaptureProperties.BufferSize, 1);
                    if (!cap.IsOpened())
                        return (false, 0, 0);

                    using var frame = new Mat();
                    // Birinchi kadr biroz kechikishi mumkin — bir necha marta o'qib ko'ramiz
                    for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
                    {
                        if (cap.Read(frame) && !frame.Empty())
                            return (true, frame.Width, frame.Height);
                    }
                    return (false, 0, 0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Video oqim ochishda xato: {Url}", MaskUrl(url));
                    return (false, 0, 0);
                }
                finally
                {
                    cap?.Release();
                    cap?.Dispose();
                }
            }, ct);

            var finished = await Task.WhenAny(task, Task.Delay(StreamTimeout, ct));
            if (finished != task)
            {
                // Timeout — VideoCapture fon rejimida o'zi yopiladi (FFMPEG stimeout orqali)
                _logger.LogWarning("Video oqim testi vaqt tugadi: {Url}", MaskUrl(url));
                return (false, 0, 0);
            }

            return await task;
        }

        // ===== Stream URL yasash (CameraStreamWorker bilan bir xil mantiq, plain-text parol bilan) =====
        // Afzallik: main StreamUrl → AI sub-stream → IP'dan Hikvision sub-stream (channel 102).
        private static string? BuildStreamUrl(CameraTestConnectionViewModel m)
        {
            if (IsStreamScheme(m.StreamUrl))
                return InjectCredentials(m.StreamUrl!.Trim(), m.Username, m.Password);

            if (IsStreamScheme(m.AiStreamUrl))
                return InjectCredentials(m.AiStreamUrl!.Trim(), m.Username, m.Password);

            if (!string.IsNullOrWhiteSpace(m.IpAddress))
            {
                var creds = !string.IsNullOrEmpty(m.Username)
                    ? $"{Uri.EscapeDataString(m.Username)}:{Uri.EscapeDataString(m.Password ?? "")}@"
                    : "";
                // Channel 102 — sub-stream (Hikvision/Dahua standart konventsiyasi)
                return $"rtsp://{creds}{m.IpAddress.Trim()}:{m.Port}/Streaming/Channels/102";
            }

            return null;
        }

        private static bool IsStreamScheme(string? url) =>
            !string.IsNullOrWhiteSpace(url) &&
            (url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
             || url.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase)
             || url.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        private static string InjectCredentials(string streamUrl, string? username, string? password)
        {
            try
            {
                var uri = new Uri(streamUrl);
                if (!string.IsNullOrEmpty(uri.UserInfo)) return streamUrl; // login allaqachon bor
                if (string.IsNullOrEmpty(username)) return streamUrl;

                var creds = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password ?? "")}";
                return $"{uri.Scheme}://{creds}@{uri.Authority}{uri.PathAndQuery}";
            }
            catch
            {
                return streamUrl;
            }
        }

        private static bool TryParseHostPort(string url, out string host, out int port)
        {
            host = string.Empty;
            port = 0;
            try
            {
                var uri = new Uri(url);
                host = uri.Host;
                port = uri.Port > 0 ? uri.Port : DefaultPortForScheme(uri.Scheme);
                return !string.IsNullOrEmpty(host) && port is > 0 and <= 65535;
            }
            catch
            {
                return false;
            }
        }

        private static int DefaultPortForScheme(string scheme) => scheme.ToLowerInvariant() switch
        {
            "rtsp" => 554,
            "rtmp" => 1935,
            "rtmps" => 1935,
            "http" => 80,
            "https" => 443,
            _ => 554
        };

        private static string MaskUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (string.IsNullOrEmpty(uri.UserInfo)) return url;
                return $"{uri.Scheme}://***:***@{uri.Authority}{uri.PathAndQuery}";
            }
            catch { return url; }
        }
    }
}
