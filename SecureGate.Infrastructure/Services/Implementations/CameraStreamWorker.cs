using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    // Har bir kamera uchun ALOHIDA THREAD (Task.Factory.StartNew + LongRunning) —
    // VideoCapture.Read bloklovchi chaqiruv bo'lgani uchun thread pool'ni band qilmaslik kerak.
    //
    // Kameralar ro'yxati har 30s yangilanadi:
    //   - yangilari ishga tushadi
    //   - o'chirilganlari to'xtaydi
    //   - sozlamalari o'zgarganlari qayta ishga tushadi (signature taqqoslash)
    //   - o'lib qolgan loop'lar (Task.IsCompleted) qayta tiklanadi
    public class CameraStreamWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFaceRecognitionEngine _engine;
        private readonly IKnownFaceCache _knownCache;
        private readonly IStreamUrlBuilder _urlBuilder;
        private readonly IHubContext<CameraHub> _cameraHub;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<CameraStreamWorker> _logger;

        // Active per-camera threads
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
        private readonly int _snapshotMaxWidth;
        private readonly int _snapshotQuality;
        private readonly int _snapshotRetentionDays;

        // Snapshot retention — kuniga bir marta ishlaydi
        private DateTime _lastRetentionRunUtc = DateTime.MinValue;

        public CameraStreamWorker(
            IServiceScopeFactory scopeFactory,
            IFaceRecognitionEngine engine,
            IKnownFaceCache knownCache,
            IStreamUrlBuilder urlBuilder,
            IHubContext<CameraHub> cameraHub,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<CameraStreamWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _engine = engine;
            _knownCache = knownCache;
            _urlBuilder = urlBuilder;
            _cameraHub = cameraHub;
            _env = env;
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

            // Snapshot hajmi va saqlash muddati
            _snapshotMaxWidth = config.GetValue<int?>("FaceRecognition:SnapshotMaxWidth") ?? SnapshotImage.DefaultMaxWidth;
            _snapshotQuality = config.GetValue<int?>("FaceRecognition:SnapshotJpegQuality") ?? SnapshotImage.DefaultQuality;
            _snapshotRetentionDays = config.GetValue<int?>("FaceRecognition:SnapshotRetentionDays") ?? 30;
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

            // Birinchi marta known faces cache to'lishini KUTAMIZ (force: true).
            // Xato bo'lsa worker o'lmasligi kerak — keyingi davriy reload cache'ni to'ldiradi.
            try
            {
                await _knownCache.ReloadAsync(stoppingToken, force: true);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Startda KnownFaceCache yuklanmadi — worker davom etadi (cache keyinroq to'ladi)");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshCamerasAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kameralarni yangilashda xato");
                }

                try
                {
                    RunSnapshotRetentionIfDue();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Snapshot retention bosqichida xato");
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

            var camerasById = cameras
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

            // 1) To'xtatish/qayta ishga tushirish kerak bo'lganlar
            foreach (var pair in _streams.ToArray())
            {
                var handle = pair.Value;

                string? reason = null;
                if (!camerasById.TryGetValue(pair.Key, out var cam))
                    reason = "ro'yxatdan chiqdi";
                else if (!string.Equals(handle.Signature, BuildSignature(cam), StringComparison.Ordinal))
                    reason = "sozlamalari o'zgardi";
                else if (handle.Task.IsCompleted)
                    reason = handle.Task.IsFaulted ? "loop xato bilan tugagan" : "loop to'xtab qolgan";

                if (reason != null)
                    StopStream(pair.Key, handle, reason);
            }

            // 2) Ishlamayotganlarini ishga tushirish (yangilar + qayta tiklanadiganlar)
            foreach (var cam in camerasById.Values)
            {
                if (_streams.ContainsKey(cam.Id)) continue;
                StartStream(cam, ct);
            }
        }

        private void StartStream(Camera cam, CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            CameraStreamHandle? handle = null;
            try
            {
                // LongRunning → thread pool'dan EMAS, alohida thread'da ishlaydi.
                // Delegat async bo'lgani uchun Unwrap() bilan ichki Task'ni olamiz.
                var task = Task.Factory.StartNew(
                        () => RunCameraLoopAsync(cam, cts.Token),
                        cts.Token,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap();

                handle = new CameraStreamHandle(cts, task, BuildSignature(cam));
                _streams[cam.Id] = handle;
                _logger.LogInformation("Kamera #{Id} ({Name}) oqimi boshlandi", cam.Id, cam.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kamera #{Id} oqimini ishga tushirishda xato", cam.Id);
                if (handle == null) cts.Dispose();
            }
        }

        private void StopStream(int cameraId, CameraStreamHandle handle, string reason)
        {
            _streams.TryRemove(new KeyValuePair<int, CameraStreamHandle>(cameraId, handle));
            handle.Cancel();
            handle.DisposeWhenCompleted();
            _logger.LogInformation("Kamera #{Id} oqimi to'xtatildi ({Reason})", cameraId, reason);
        }

        // Kameraning oqimga ta'sir qiluvchi maydonlaridan barmoq izi.
        // O'zgarsa — oqim to'xtatilib qayta ishga tushiriladi.
        private static string BuildSignature(Camera cam)
        {
            var raw = string.Join('|',
                cam.StreamUrl ?? "",
                cam.AiStreamUrl ?? "",
                cam.Username ?? "",
                cam.Password ?? "",
                ((int)cam.Type).ToString(CultureInfo.InvariantCulture),
                cam.IpAddress ?? "",
                cam.Port.ToString(CultureInfo.InvariantCulture),
                // Vendor/NVR maydonlari ham URL'ga ta'sir qiladi -> ular o'zgarsa oqim qayta ishga tushishi kerak.
                ((int)cam.CameraModel).ToString(CultureInfo.InvariantCulture),
                ((int)cam.DeviceKind).ToString(CultureInfo.InvariantCulture),
                cam.ChannelNumber?.ToString(CultureInfo.InvariantCulture) ?? "");

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        // Bir kamera uchun davomli loop. RTSP ochiladi, frame'lar o'qiladi.
        // Xato bo'lsa qisqa pauza qilib qayta ulanishga urinamiz.
        private async Task RunCameraLoopAsync(Camera cam, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                string streamUrl;
                try
                {
                    // URL yasash ham xato berishi mumkin (parol deshifrlash, noto'g'ri URI) —
                    // bu loop'ni butunlay o'ldirmasligi kerak.
                    // Yuz tanish uchun SUB-stream afzal (AiStreamUrl -> StreamUrl -> vendor shabloni).
                    streamUrl = _urlBuilder.BuildLive(cam, StreamPurpose.Sub) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(streamUrl))
                    {
                        _logger.LogError("Kamera #{Id} uchun oqim URL yasab bo'lmadi", cam.Id);
                        await Task.Delay(TimeSpan.FromSeconds(30), ct);
                        continue;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Kamera #{Id} uchun oqim URL yasashda xato", cam.Id);
                    try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                try
                {
                    using var capture = new VideoCapture(streamUrl);

                    // DIQQAT: CAP_PROP_BUFFERSIZE OpenCV'ning FFMPEG backend'ida (RTSP/HTTP)
                    // UMUMAN QO'LLANMAYDI — u faqat DSHOW / V4L2 / GStreamer backend'larida ishlaydi.
                    // Ya'ni bu chaqiruv RTSP uchun hech narsa qilmaydi va lag'dan HIMOYA QILMAYDI;
                    // unga tayanib bo'lmaydi. O'chirilmadi: boshqa backend (lokal USB kamera va h.k.)
                    // ishlatilsa foydali bo'ladi.
                    // Kechikishga qarshi haqiqiy himoya:
                    //   1) OpenCvBootstrap.Configure() dagi fflags;nobuffer / flags;low_delay /
                    //      max_delay / reorder_queue_size FFMPEG flaglari;
                    //   2) bu yerda kadr o'qish sikli HECH QACHON uxlamaydi — throttle faqat
                    //      ProcessFrameAsync chaqirig'ini cheklaydi, Read esa har iteratsiyada
                    //      bajariladi, shuning uchun demuxer navbati to'planib qolmaydi.
                    capture.Set(VideoCaptureProperties.BufferSize, 1);

                    if (!capture.IsOpened())
                    {
                        _logger.LogWarning("Kamera #{Id} ulanmadi: {Url}", cam.Id, _urlBuilder.Mask(streamUrl));
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
            byte[]? snapshotBytes = null;   // faqat kerak bo'lganda (dispatch paytida) yasaladi
            int frameWidth, frameHeight;
            try
            {
                // Aniqlash uchun TO'LIQ o'lchamli kadr kerak (kichraytirilsa yuz topilmay qoladi).
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
            catch (OperationCanceledException) { return; }
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

                // Snapshot diskka yoziladi → kichraytirilgan/siqilgan variantni uzatamiz.
                // Bir frame ichida bir marta hisoblanadi va barcha yuzlar uchun qayta ishlatiladi.
                snapshotBytes ??= EncodeSnapshot(frame, cam.Id);

                var ev = new FaceMatchEvent(
                    cam.Id, personType, personId, fullName, confidence,
                    snapshotBytes, face.Box, frameWidth, frameHeight);

                // Kamera turi bo'yicha yo'naltiramiz:
                //   Turnstile → AccessLog + turniket ochish (FaceMatchHandler)
                //   Regular   → faqat CameraUser kuzatuv yozuvi (CameraSightingHandler)
                await DispatchAsync(cam.Type, ev, ct);
            }

            // Frame oxirida — frontend qutilarni darhol sinxronlashi uchun
            await NotifyFrameProcessedAsync(cam.Id, activePersonKeys, ct);
        }

        // Diskka yoziladigan snapshot: maks 640px kenglik, JPEG q75 (config'dan sozlanadi).
        private byte[] EncodeSnapshot(Mat frame, int cameraId)
        {
            try
            {
                return SnapshotImage.Encode(frame, _snapshotMaxWidth, _snapshotQuality);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Snapshot kodlashda xato (camera #{Id})", cameraId);
                return Array.Empty<byte>();
            }
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
                    // Tarmoqda HAMMA VAQT UTC, ISO-8601 ("O") — formatlash frontend ishi.
                    time = DateTime.UtcNow.ToString("O")
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yuz handler'ini chaqirishda xato (camera #{Id}, type {Type})", ev.CameraId, cameraType);
            }
        }

        // ===== Snapshot retention =====
        // Eski snapshot fayllari cheksiz to'planmasligi uchun kuniga bir marta tozalash.
        // Config: FaceRecognition:SnapshotRetentionDays (default 30; 0 yoki manfiy — o'chirilgan).
        private void RunSnapshotRetentionIfDue()
        {
            if (_snapshotRetentionDays <= 0) return;
            if ((DateTime.UtcNow - _lastRetentionRunUtc).TotalHours < 24) return;
            _lastRetentionRunUtc = DateTime.UtcNow;

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot)) return;

            var dir = Path.Combine(webRoot, "uploads", "snapshots");
            if (!Directory.Exists(dir)) return;

            var cutoff = DateTime.UtcNow.AddDays(-_snapshotRetentionDays);
            int deleted = 0, failed = 0;

            foreach (var file in Directory.EnumerateFiles(dir, "*.jpg", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) >= cutoff) continue;
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    failed++;
                }
            }

            if (deleted > 0 || failed > 0)
                _logger.LogInformation("Snapshot retention: {Deleted} ta fayl o'chirildi, {Failed} ta xato ({Days} kundan eski)",
                    deleted, failed, _snapshotRetentionDays);
        }

        private async Task StopAllAsync()
        {
            var handles = _streams.ToArray();

            foreach (var pair in handles)
                pair.Value.Cancel();

            foreach (var pair in handles)
            {
                try { await pair.Value.Task; } catch { }
                pair.Value.Dispose();
            }

            _streams.Clear();
        }

        public override void Dispose()
        {
            foreach (var pair in _streams.ToArray())
            {
                pair.Value.Cancel();
                pair.Value.DisposeWhenCompleted();
            }
            _streams.Clear();
            base.Dispose();
        }

        private sealed class CameraStreamHandle : IDisposable
        {
            private int _disposed;

            public CancellationTokenSource Cts { get; }
            public Task Task { get; }

            /// <summary>Kamera sozlamalarining barmoq izi — o'zgarsa oqim qayta ishga tushiriladi.</summary>
            public string Signature { get; }

            public CameraStreamHandle(CancellationTokenSource cts, Task task, string signature)
            {
                Cts = cts;
                Task = task;
                Signature = signature;
            }

            public void Cancel()
            {
                try { Cts.Cancel(); } catch { }
            }

            /// <summary>Loop tugagach CTS'ni dispose qiladi (loop hali ishlatayotgan bo'lishi mumkin).</summary>
            public void DisposeWhenCompleted()
            {
                Task.ContinueWith(_ => Dispose(), TaskScheduler.Default);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { Cts.Dispose(); } catch { }
            }
        }
    }
}
