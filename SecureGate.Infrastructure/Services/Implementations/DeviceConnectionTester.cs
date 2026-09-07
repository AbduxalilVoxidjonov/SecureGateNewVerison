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

        private readonly IStreamUrlBuilder _urlBuilder;
        private readonly ILogger<DeviceConnectionTester> _logger;

        public DeviceConnectionTester(IStreamUrlBuilder urlBuilder, ILogger<DeviceConnectionTester> logger)
        {
            _urlBuilder = urlBuilder;
            _logger = logger;
        }

        public async Task<ConnectionTestResult> TestCameraAsync(CameraTestConnectionViewModel model, CancellationToken ct = default)
        {
            // Test — ko'rsatish bilan bir xil mantiq: MAIN stream afzal.
            // Parol bu yerda OCHIQ matnda keladi (hali shifrlanmagan), shu sababli
            // Camera entity'si emas, StreamEndpoint ishlatiladi.
            var url = _urlBuilder.BuildLive(ToEndpoint(model), StreamPurpose.Main);
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
            // FFMPEG capture sozlamalari (rtsp_transport, stimeout) process-global —
            // ular OpenCvBootstrap.Configure() orqali Program.cs da bir marta o'rnatiladi.
            // Bu yerda qayta yozish parallel oqimlar bilan poyga hosil qilardi.

            // VideoCapture bloklovchi chaqiruv — Task.Run + umumiy timeout bilan o'raymiz,
            // shunda so'rov hech qachon osilib qolmaydi.
            var task = Task.Run(() =>
            {
                VideoCapture? cap = null;
                try
                {
                    cap = new VideoCapture(url);

                    // DIQQAT: CAP_PROP_BUFFERSIZE FFMPEG backend'ida (RTSP/HTTP) UMUMAN QO'LLANMAYDI —
                    // faqat DSHOW / V4L2 / GStreamer'da ishlaydi. Bu yerda u zararsiz, lekin foydasiz.
                    // Ulanish testi uchun kechikish muhim emas, shuning uchun boshqa chora kerak emas.
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
                    _logger.LogWarning(ex, "Video oqim ochishda xato: {Url}", _urlBuilder.Mask(url));
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
                _logger.LogWarning("Video oqim testi vaqt tugadi: {Url}", _urlBuilder.Mask(url));
                return (false, 0, 0);
            }

            return await task;
        }

        // ===== Forma ma'lumotlarini URL builder tushunadigan ko'rinishga o'tkazish =====
        // Model/kanal maydonlari test formasida yo'q — shablon kerak bo'lganda
        // default (Hikvision, kanal 1) ishlatiladi, ya'ni eski xatti-harakat saqlanadi.
        private static StreamEndpoint ToEndpoint(CameraTestConnectionViewModel m) => new()
        {
            StreamUrl = m.StreamUrl,
            AiStreamUrl = m.AiStreamUrl,
            IpAddress = m.IpAddress,
            Port = m.Port,
            Username = m.Username,
            Password = m.Password,
            CameraModel = CameraModel.Hikvision,
            ChannelNumber = 1
        };

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

    }
}
