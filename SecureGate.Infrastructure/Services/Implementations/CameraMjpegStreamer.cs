using System.Text;
using OpenCvSharp;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // Kamera oqimini brauzerga uzatadi. Yuz tanish (CameraStreamWorker) bilan alohida —
    // bu xizmat faqat ko'rsatish (viewing) uchun, kerak bo'lganda ochilib, mijoz ketganda yopiladi.
    public class CameraMjpegStreamer : ICameraMjpegStreamer
    {
        private const int TargetFps = 12;
        private const int JpegQuality = 70;
        private const string Boundary = "frame";

        private readonly ICameraCredentialProtector _cred;
        private readonly ILogger<CameraMjpegStreamer> _logger;

        public CameraMjpegStreamer(ICameraCredentialProtector cred, ILogger<CameraMjpegStreamer> logger)
        {
            _cred = cred;
            _logger = logger;
        }

        public async Task StreamAsync(Camera camera, Stream output, int? maxWidth, CancellationToken ct)
        {
            var url = BuildViewingUrl(camera);
            if (string.IsNullOrWhiteSpace(url))
                return;

            ApplyFfmpegOptions();

            using var capture = new VideoCapture(url);
            capture.Set(VideoCaptureProperties.BufferSize, 1);
            if (!capture.IsOpened())
            {
                _logger.LogWarning("Kamera #{Id} ko'rsatish uchun ochilmadi: {Url}", camera.Id, MaskUrl(url));
                return;
            }

            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / TargetFps);
            var emptyReads = 0;
            using var frame = new Mat();

            while (!ct.IsCancellationRequested)
            {
                if (!capture.Read(frame) || frame.Empty())
                {
                    if (++emptyReads > 50) break; // oqim uzildi
                    await Task.Delay(80, ct);
                    continue;
                }
                emptyReads = 0;

                var jpeg = EncodeJpeg(frame, maxWidth);

                // multipart bo'lak: --frame\r\nContent-Type...\r\n\r\n<bytes>\r\n
                var header = $"--{Boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n";
                await output.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
                await output.WriteAsync(jpeg, ct);
                await output.WriteAsync("\r\n"u8.ToArray(), ct);
                await output.FlushAsync(ct);

                await Task.Delay(frameInterval, ct);
            }
        }

        public async Task<byte[]?> SnapshotAsync(Camera camera, int? maxWidth, CancellationToken ct)
        {
            var url = BuildViewingUrl(camera);
            if (string.IsNullOrWhiteSpace(url))
                return null;

            ApplyFfmpegOptions();

            return await Task.Run<byte[]?>(() =>
            {
                using var capture = new VideoCapture(url);
                capture.Set(VideoCaptureProperties.BufferSize, 1);
                if (!capture.IsOpened())
                    return null;

                using var frame = new Mat();
                for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
                {
                    if (capture.Read(frame) && !frame.Empty())
                        return EncodeJpeg(frame, maxWidth);
                }
                return null;
            }, ct);
        }

        // ===== JPEG kodlash (kerak bo'lsa kichraytirib) =====
        private static byte[] EncodeJpeg(Mat frame, int? maxWidth)
        {
            var prm = new ImageEncodingParam(ImwriteFlags.JpegQuality, JpegQuality);

            if (maxWidth.HasValue && maxWidth.Value > 0 && frame.Width > maxWidth.Value)
            {
                var scale = maxWidth.Value / (double)frame.Width;
                using var resized = new Mat();
                Cv2.Resize(frame, resized, new Size(maxWidth.Value, (int)(frame.Height * scale)));
                return resized.ImEncode(".jpg", prm);
            }

            return frame.ImEncode(".jpg", prm);
        }

        private static void ApplyFfmpegOptions()
        {
            // FFMPEG cheksiz kutib qolmasligi uchun TCP transport + socket timeout (5s).
            Environment.SetEnvironmentVariable(
                "OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp|stimeout;5000000");
        }

        // Ko'rsatish uchun URL: main StreamUrl afzal (sifatli) → AI sub-stream → IP'dan yasash.
        private string? BuildViewingUrl(Camera cam)
        {
            if (IsStreamScheme(cam.StreamUrl))
                return InjectCredentials(cam.StreamUrl!.Trim(), cam);

            if (IsStreamScheme(cam.AiStreamUrl))
                return InjectCredentials(cam.AiStreamUrl!.Trim(), cam);

            if (!string.IsNullOrWhiteSpace(cam.IpAddress))
            {
                var user = cam.Username ?? "";
                var pass = _cred.Unprotect(cam.Password) ?? "";
                var creds = !string.IsNullOrEmpty(user)
                    ? $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}@"
                    : "";
                // Channel 101 — main stream (ko'rsatish uchun sifatliroq)
                return $"rtsp://{creds}{cam.IpAddress.Trim()}:{cam.Port}/Streaming/Channels/101";
            }

            return null;
        }

        private static bool IsStreamScheme(string? url) =>
            !string.IsNullOrWhiteSpace(url) &&
            (url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
             || url.StartsWith("rtmp", StringComparison.OrdinalIgnoreCase)
             || url.StartsWith("http", StringComparison.OrdinalIgnoreCase));

        private string InjectCredentials(string streamUrl, Camera cam)
        {
            try
            {
                var uri = new Uri(streamUrl);
                if (!string.IsNullOrEmpty(uri.UserInfo)) return streamUrl; // login allaqachon bor
                if (string.IsNullOrEmpty(cam.Username)) return streamUrl;

                var pass = _cred.Unprotect(cam.Password) ?? "";
                var creds = $"{Uri.EscapeDataString(cam.Username)}:{Uri.EscapeDataString(pass)}";
                return $"{uri.Scheme}://{creds}@{uri.Authority}{uri.PathAndQuery}";
            }
            catch
            {
                return streamUrl;
            }
        }

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
