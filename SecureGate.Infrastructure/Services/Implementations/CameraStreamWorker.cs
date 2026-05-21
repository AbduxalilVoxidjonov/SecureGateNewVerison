using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using SecureGate.Data;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // BackgroundService — DB'dagi faol kameralarning RTSP oqimini ochib turadi,
    // frame'larni o'qiydi, FaceRecognitionEngine orqali yuzlarni aniqlaydi va
    // KnownFaceCache bilan solishtiradi. Mosini topsa — FaceMatchHandler chaqiradi.
    //
    // Har bir kamera uchun alohida Task (alohida thread emas — Task continuation).
    // Kameralar ro'yxati har 30s yangilanadi: yangilari ishga tushadi, o'chirilganlari to'xtaydi.
    public class CameraStreamWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFaceRecognitionEngine _engine;
        private readonly IKnownFaceCache _knownCache;
        private readonly ICameraCredentialProtector _credProtector;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly IConfiguration _config;
        private readonly ILogger<CameraStreamWorker> _logger;

        // Active per-camera tasks
        private readonly ConcurrentDictionary<int, CameraStreamHandle> _streams = new();

        // DB yozish cooldown'i: (cameraId, personKey) → oxirgi yozuv UTC.
        // SignalR notification cooldown'i AKS — har frame'da yashil quti yangilanishi uchun.
        // Turniket va oddiy kamera uchun alohida cooldown qiymatlari qo'llaniladi (camera turiga qarab).
        private readonly ConcurrentDictionary<string, DateTime> _lastDispatchAt = new();

        // Settings (config'dan)
        private readonly float _minSimilarity;
        private readonly float _minMatchMargin;
        private readonly int _detectionIntervalMs;
        private readonly int _turnstileCooldownSeconds;
        private readonly int _regularCooldownSeconds;
        private readonly int _cameraRefreshSeconds;

        public CameraStreamWorker(
            IServiceScopeFactory scopeFactory,
            IFaceRecognitionEngine engine,
            IKnownFaceCache knownCache,
            ICameraCredentialProtector credProtector,
            IHubContext<CameraHub> cameraHub,
            IConfiguration config,
            ILogger<CameraStreamWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _engine = engine;
            _knownCache = knownCache;
            _credProtector = credProtector;
            _cameraHub = cameraHub;
            _config = config;
            _logger = logger;

            _minSimilarity = (float)(config.GetValue<double?>("FaceRecognition:MinSimilarity") ?? 0.6);
            _minMatchMargin = (float)(config.GetValue<double?>("FaceRecognition:MinMatchMargin") ?? 0.05);
            _detectionIntervalMs = config.GetValue<int?>("FaceRecognition:DetectionIntervalMs") ?? 500;

            // Turniket kamerasi uchun cooldown — kichik (default 3s) shunda turniket tez ochilib yopiladi.
            _turnstileCooldownSeconds = config.GetValue<int?>("FaceRecognition:TurnstileCooldownSeconds") ?? 3;
            // Oddiy kamera uchun cooldown — kichik (default 3s) shunda kim qaraganligi tez-tez yoziladi.
            _regularCooldownSeconds = config.GetValue<int?>("FaceRecognition:RegularCooldownSeconds") ?? 3;
            // Eski "AccessLogCooldownSeconds" sozlamasi orqaga moslik uchun ham o'qiladi —
            // agar config'da bo'lsa, turniket kamerasi uchun ishlatiladi.
            var legacy = config.GetValue<int?>("FaceRecognition:AccessLogCooldownSeconds");
            if (legacy.HasValue) _turnstileCooldownSeconds = legacy.Value;

            _cameraRefreshSeconds = config.GetValue<int?>("FaceRecognition:CameraRefreshSeconds") ?? 30;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _config.GetValue<bool?>("FaceRecognition:EnabledOnStartup") ?? true;
            if (!enabled)
            {
                _logger.LogInformation("CameraStreamWorker o'chirilgan (FaceRecognition:EnabledOnStartup=false)");
                return;
            }

            _logger.LogInformation("CameraStreamWorker ishga tushdi (minSim={Sim}, interval={Int}ms)",
                _minSimilarity, _detectionIntervalMs);

            // Birinchi marta known faces cache to'lishini kutamiz
            await _knownCache.ReloadAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshCamerasAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kameralarni yangilashda xato");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_cameraRefreshSeconds), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }

            await StopAllAsync();
        }

        private async Task RefreshCamerasAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cameras = await db.Cameras
                .Where(c => c.FaceRecognitionEnabled
                    && !string.IsNullOrEmpty(c.StreamUrl)
                    && c.Status != CameraStatus.Offline)
                .AsNoTracking()
                .ToListAsync(ct);

            var activeIds = new HashSet<int>(cameras.Select(c => c.Id));

            // To'xtatish kerak bo'lganlar (ro'yxatdan tushgan yoki o'chirilgan)
            foreach (var pair in _streams.ToArray())
            {
                if (!activeIds.Contains(pair.Key))
                {
                    pair.Value.Cancel();
                    _streams.TryRemove(pair.Key, out _);
                    _logger.LogInformation("Kamera #{Id} oqimi to'xtatildi", pair.Key);
                }
            }

            // Yangilarini ishga tushirish
            foreach (var cam in cameras)
            {
                if (_streams.ContainsKey(cam.Id)) continue;

                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var task = Task.Run(() => RunCameraLoopAsync(cam, cts.Token), cts.Token);
                _streams[cam.Id] = new CameraStreamHandle(cts, task);
                _logger.LogInformation("Kamera #{Id} ({Name}) oqimi boshlandi", cam.Id, cam.Name);
            }
        }

        // Bir kamera uchun davomli loop. RTSP ochiladi, frame'lar o'qiladi.
        // Xato bo'lsa qisqa pauza qilib qayta ulanishga urinamiz.
        private async Task RunCameraLoopAsync(Camera cam, CancellationToken ct)
        {
            var streamUrl = BuildStreamUrl(cam);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var capture = new VideoCapture(streamUrl);
                    // RTSP buferi katta bo'lsa lag bo'ladi; bufer hajmini kichik qilamiz
                    capture.Set(VideoCaptureProperties.BufferSize, 1);

                    if (!capture.IsOpened())
                    {
                        _logger.LogWarning("Kamera #{Id} ulanmadi: {Url}", cam.Id, MaskUrl(streamUrl));
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                        continue;
                    }

                    using var frame = new Mat();
                    var nextProcessAt = DateTime.UtcNow;

                    while (!ct.IsCancellationRequested && capture.IsOpened())
                    {
                        if (!capture.Read(frame) || frame.Empty())
                        {
                            await Task.Delay(200, ct);
                            continue;
                        }

                        // Throttle: faqat har _detectionIntervalMs ms da frame'ni qayta ishlaymiz
                        if (DateTime.UtcNow < nextProcessAt) continue;
                        nextProcessAt = DateTime.UtcNow.AddMilliseconds(_detectionIntervalMs);

                        await ProcessFrameAsync(cam, frame, ct);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kamera #{Id} oqimida xato", cam.Id);
                    try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                    catch (OperationCanceledException) { break; }
                }
            }

            _logger.LogInformation("Kamera #{Id} loop tugadi", cam.Id);
        }

        private async Task ProcessFrameAsync(Camera cam, Mat frame, CancellationToken ct)
        {
            byte[] jpegBytes;
            int frameWidth, frameHeight;
            try
            {
                jpegBytes = frame.ImEncode(".jpg");
                frameWidth = frame.Width;
                frameHeight = frame.Height;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Frame'ni JPG'ga o'tkazishda xato (camera #{Id})", cam.Id);
                return;
            }

            IReadOnlyList<DetectedFace> faces;
            try
            {
                faces = await _engine.DetectFacesAsync(jpegBytes, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yuz aniqlashda xato (camera #{Id})", cam.Id);
                return;
            }

            // Frame'dagi aktiv odamlar ro'yxati (frontend qutilarni darhol tozalashi uchun).
            // Yuz yo'q bo'lsa ham bo'sh ro'yxat yuboriladi — frontend eski qutilarni o'chiradi.
            var activePersonKeys = new List<string>(faces.Count);

            if (faces.Count == 0)
            {
                await NotifyFrameProcessedAsync(cam.Id, activePersonKeys, ct);
                return;
            }

            var known = _knownCache.Snapshot;
            foreach (var face in faces)
            {
                var (best, bestSim, secondSim) = FindBestMatch(face.Embedding, known);

                string personType;
                int? personId;
                string fullName;
                float confidence;

                // Tanish quyidagi 2 shartni qondirishi kerak:
                //   1) bestSim >= _minSimilarity (0.6 default) — yetarlicha o'xshash
                //   2) bestSim - secondSim >= _minMatchMargin — ikkinchi nomzoddan farqlanadigan
                // Margin sharti chegara ustidagi ambiguous holatlarni "Unknown" qiladi
                // (masalan: bestSim=0.62 va secondSim=0.60 → ikkalasi ham yaqin → ishonchsiz).
                bool matched = best != null
                    && bestSim >= _minSimilarity
                    && (bestSim - secondSim) >= _minMatchMargin;

                if (matched)
                {
                    personType = best!.PersonType;
                    personId = best.PersonId;
                    fullName = best.FullName;
                    confidence = bestSim;
                }
                else
                {
                    personType = "Unknown";
                    personId = null;
                    fullName = "Noma'lum";
                    confidence = Math.Max(0f, bestSim);
                }

                // 1) Yashil quti uchun SignalR — har frame'da yuboriladi (cooldownsiz)
                await NotifyFaceDetectedAsync(cam.Id, personType, personId, fullName, confidence,
                    face.Box, frameWidth, frameHeight, ct);

                // Frontend frame state sinxronizatsiyasi uchun aktiv key
                activePersonKeys.Add(matched ? $"{personType}:{personId}" : "Unknown");

                // 2) DB yozish (AccessLog yoki CameraUser) — kamera turiga qarab cooldown tanlanadi:
                //    Turnstile — _turnstileCooldownSeconds (default 3s, turniket tez ochilsin)
                //    Regular   — _regularCooldownSeconds  (default 3s, kuzatuv tez-tez yozilsin)
                var cooldownSeconds = cam.Type == CameraType.Turnstile
                    ? _turnstileCooldownSeconds
                    : _regularCooldownSeconds;

                var key = $"{cam.Id}:{personType}:{personId?.ToString() ?? "Unknown"}";
                var now = DateTime.UtcNow;
                if (_lastDispatchAt.TryGetValue(key, out var last) && (now - last).TotalSeconds < cooldownSeconds)
                    continue;
                _lastDispatchAt[key] = now;

                var ev = new FaceMatchEvent(
                    cam.Id, personType, personId, fullName, confidence,
                    jpegBytes, face.Box, frameWidth, frameHeight);

                // Kamera turi bo'yicha yo'naltiramiz:
                //   Turnstile → AccessLog + turniket ochish (FaceMatchHandler)
                //   Regular   → faqat CameraUser kuzatuv yozuvi (CameraSightingHandler)
                await DispatchAsync(cam.Type, ev, ct);
            }

            // Frame oxirida — frontend qutilarni darhol sinxronlashi uchun
            await NotifyFrameProcessedAsync(cam.Id, activePersonKeys, ct);
        }

        // Har frame'da chaqiriladi — UI yashil quti uzluksiz yangilanishi uchun.
        // DB yozmaydi, faqat SignalR event yuboradi.
        private async Task NotifyFaceDetectedAsync(
            int cameraId, string personType, int? personId, string fullName, float confidence,
            BoundingBox box, int frameWidth, int frameHeight, CancellationToken ct)
        {
            try
            {
                await _cameraHub.Clients.All.SendAsync("FaceDetected", new
                {
                    cameraId,
                    name = fullName,
                    personType,
                    personId,
                    confidence,
                    isUnknown = personType == "Unknown",
                    box = new
                    {
                        x = box.X,
                        y = box.Y,
                        w = box.Width,
                        h = box.Height,
                        fw = frameWidth,
                        fh = frameHeight
                    },
                    time = DateTime.Now.ToString("HH:mm:ss")
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR FaceDetected yuborishda xato (camera #{Id})", cameraId);
            }
        }

        // Frame oxirida yuboriladi — frame'da hozir aktiv bo'lgan odamlar ro'yxati.
        // Frontend bu ro'yxatda bo'lmagan qutilarni darhol o'chiradi (TTL kutmasdan).
        // Yuz yo'q bo'lsa, bo'sh ro'yxat yuboriladi — barcha qutilar tozalanadi.
        private async Task NotifyFrameProcessedAsync(int cameraId, IList<string> activePersonKeys, CancellationToken ct)
        {
            try
            {
                await _cameraHub.Clients.All.SendAsync("FaceFrameProcessed", new
                {
                    cameraId,
                    activeKeys = activePersonKeys
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR FaceFrameProcessed yuborishda xato (camera #{Id})", cameraId);
            }
        }

        // Eng yaqin va ikkinchi eng yaqin nomzodlarni topadi.
        // Ikkinchi nomzod margin sharti uchun kerak (bestSim - secondSim chegarasini tekshirish).
        private (KnownFace? Best, float BestSimilarity, float SecondSimilarity) FindBestMatch(
            float[] probe, IReadOnlyList<KnownFace> known)
        {
            KnownFace? best = null;
            float bestSim = -1f;
            float secondSim = -1f;
            for (int i = 0; i < known.Count; i++)
            {
                var sim = _engine.Similarity(probe, known[i].Embedding);
                if (sim > bestSim)
                {
                    secondSim = bestSim;
                    bestSim = sim;
                    best = known[i];
                }
                else if (sim > secondSim)
                {
                    secondSim = sim;
                }
            }
            return (best, bestSim, secondSim);
        }

        private async Task DispatchAsync(CameraType cameraType, FaceMatchEvent ev, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                if (cameraType == CameraType.Turnstile)
                {
                    // Turniket — AccessLog + turniket ochish
                    var handler = scope.ServiceProvider.GetRequiredService<IFaceMatchHandler>();
                    await handler.HandleAsync(ev, ct);
                }
                else
                {
                    // Oddiy — faqat CameraUser yozuvi
                    var handler = scope.ServiceProvider.GetRequiredService<ICameraSightingHandler>();
                    await handler.HandleAsync(ev, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yuz handler'ini chaqirishda xato (camera #{Id}, type {Type})", ev.CameraId, cameraType);
            }
        }

        // Yuz tanish uchun qaysi oqimni ishlatish kerakligini hal qiladi:
        //   1) AiStreamUrl mavjud bo'lsa — uni ishlatamiz (sub-stream, 480p/720p — CPU/GPU tejaladi)
        //   2) Aks holda StreamUrl — main stream (FullHD/4K)
        //   3) StreamUrl ham yo'q bo'lsa — IP/Port'dan Hikvision uslubida yasaymiz (sub-stream — channel 102)
        // 500+ kamerali deployment'larda AiStreamUrl ishlatish ishlash unumdorligini sezilarli yaxshilaydi.
        private string BuildStreamUrl(Camera cam)
        {
            // Variant 1: AI sub-stream (afzallik)
            if (!string.IsNullOrWhiteSpace(cam.AiStreamUrl)
                && (cam.AiStreamUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
                    || cam.AiStreamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            {
                return InjectCredentials(cam.AiStreamUrl, cam);
            }

            // Variant 2: main stream (faqat AI sub-stream yo'q bo'lganda)
            if (!string.IsNullOrWhiteSpace(cam.StreamUrl)
                && (cam.StreamUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
                    || cam.StreamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            {
                return InjectCredentials(cam.StreamUrl, cam);
            }

            // Variant 3: IpAddress'dan yasaymiz — Hikvision sub-stream (channel 102 = 480p/720p)
            var user = cam.Username ?? "";
            var pass = _credProtector.Unprotect(cam.Password) ?? "";
            var creds = !string.IsNullOrEmpty(user) ? $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}@" : "";
            // Channel 102 — sub-stream (Hikvision/Dahua standart konventsiyasi)
            return $"rtsp://{creds}{cam.IpAddress}:{cam.Port}/Streaming/Channels/102";
        }

        // Agar StreamUrl'da login bo'lmasa, DB'dagi shifrlangan parolni qo'shamiz.
        private string InjectCredentials(string streamUrl, Camera cam)
        {
            try
            {
                var uri = new Uri(streamUrl);
                if (!string.IsNullOrEmpty(uri.UserInfo)) return streamUrl;

                var user = cam.Username;
                if (string.IsNullOrEmpty(user)) return streamUrl;

                var pass = _credProtector.Unprotect(cam.Password) ?? "";
                var creds = $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}";

                var builder = new UriBuilder(uri) { UserName = "" };
                // UriBuilder shaxsiy ma'lumotni "user:pass" qilib qo'shishni yoqtirmaydi —
                // qo'lda yasaymiz.
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

        private async Task StopAllAsync()
        {
            foreach (var pair in _streams.ToArray())
            {
                pair.Value.Cancel();
            }

            foreach (var pair in _streams.ToArray())
            {
                try { await pair.Value.Task; } catch { }
            }

            _streams.Clear();
        }

        private sealed class CameraStreamHandle
        {
            public CancellationTokenSource Cts { get; }
            public Task Task { get; }

            public CameraStreamHandle(CancellationTokenSource cts, Task task)
            {
                Cts = cts;
                Task = task;
            }

            public void Cancel()
            {
                try { Cts.Cancel(); } catch { }
            }
        }
    }
}
