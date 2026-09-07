using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using OpenCvSharp;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    /// <summary>
    /// Kamera oqimini brauzerga uzatadi (MJPEG). Yuz tanish (CameraStreamWorker) bilan alohida —
    /// bu xizmat faqat ko'rsatish (viewing) uchun.
    ///
    /// <para>
    /// <b>SINGLETON</b> bo'lishi shart (Program.cs: <c>AddSingleton&lt;ICameraMjpegStreamer, CameraMjpegStreamer&gt;()</c>).
    /// Har bir kamera uchun BITTA <see cref="VideoCapture"/> ochiladi (bitta "broadcaster"),
    /// undagi alohida LongRunning producer thread kadrlarni o'qiydi va barcha mijozlarga
    /// fan-out qiladi. Har mijozning o'z <see cref="Channel{T}"/> navbati bor
    /// (capacity 1, <see cref="BoundedChannelFullMode.DropOldest"/>) — sekin mijoz
    /// oqimni sekinlashtirmaydi, shunchaki kadr tashlab yuboradi.
    /// </para>
    ///
    /// <para>
    /// Mijozlar ref-count qilinadi: oxirgi mijoz ketganda capture yopiladi va
    /// broadcaster lug'atdan olib tashlanadi.
    /// </para>
    ///
    /// <para>
    /// <b>KECHIKISH (latency) haqida — eng muhim qism.</b>
    /// Ilgari producer sikli har kadrdan keyin <c>1000/TargetFps</c> ms uxlardi (TargetFps=12 → 83 ms).
    /// Kamera 25 fps yuborsa, sekundiga ~13 ta kadr FFMPEG demuxer navbatida to'planardi va
    /// <c>capture.Read</c> tobora ESKIROQ kadr qaytarardi — kechikish cheksiz o'sardi.
    /// Endi sikl umuman uxlamaydi: har iteratsiyada navbat <c>GrabLatest()</c>
    /// bilan bo'shatiladi (cheklangan drain), shuning uchun har doim eng yangi kadr uzatiladi.
    /// FPS chegarasi (agar kerak bo'lsa) endi UXLASH bilan emas, kadrni TASHLAB YUBORISH bilan
    /// amalga oshiriladi — bu navbat to'planishiga olib kelmaydi.
    /// </para>
    ///
    /// <para>Konfiguratsiya (appsettings.json):
    /// <list type="bullet">
    ///   <item><c>Camera:TargetFps</c> — mijozga uzatiladigan maksimal FPS (default 25; 0 = cheklovsiz).</item>
    ///   <item><c>Camera:MaxDrainFrames</c> — bir iteratsiyada ko'pi bilan nechta kadr "drain" qilinadi (default 4).</item>
    ///   <item><c>Camera:SnapshotTimeoutSeconds</c> — snapshot uchun birinchi kadrni kutish (default 8).</item>
    /// </list>
    /// </para>
    ///
    /// <para>DIQQAT: konstruktorga Scoped bog'liqlik (masalan AppDbContext) qo'shib bo'lmaydi.</para>
    /// </summary>
    public sealed class CameraMjpegStreamer : ICameraMjpegStreamer, IDisposable
    {
        private const int JpegQuality = 70;
        private const string Boundary = "frame";

        // Default qiymatlar (config bo'lmasa ishlatiladi).
        private const int DefaultTargetFps = 25;
        private const int DefaultMaxDrainFrames = 4;
        private const int DefaultSnapshotTimeoutSeconds = 8;

        private readonly IStreamUrlBuilder _urlBuilder;
        private readonly ILogger<CameraMjpegStreamer> _logger;

        private readonly int _targetFps;
        private readonly int _maxDrainFrames;
        private readonly int _snapshotTimeoutSeconds;

        private readonly ConcurrentDictionary<int, Broadcaster> _broadcasters = new();
        private volatile bool _disposed;

        public CameraMjpegStreamer(
            IStreamUrlBuilder urlBuilder,
            IConfiguration config,
            ILogger<CameraMjpegStreamer> logger)
        {
            _urlBuilder = urlBuilder;
            _logger = logger;

            // 0 = FPS cheklovi yo'q (kamera qanday bersa, shunday uzatamiz).
            _targetFps = Math.Clamp(config.GetValue<int?>("Camera:TargetFps") ?? DefaultTargetFps, 0, 60);
            _maxDrainFrames = Math.Clamp(config.GetValue<int?>("Camera:MaxDrainFrames") ?? DefaultMaxDrainFrames, 1, 16);
            _snapshotTimeoutSeconds = Math.Clamp(
                config.GetValue<int?>("Camera:SnapshotTimeoutSeconds") ?? DefaultSnapshotTimeoutSeconds, 1, 60);
        }

        // ===== MJPEG oqim =====
        public async Task StreamAsync(Camera camera, Stream output, int? maxWidth, CancellationToken ct)
        {
            // Ko'rsatish uchun MAIN stream afzal (StreamUrl -> AiStreamUrl -> vendor shabloni).
            var url = _urlBuilder.BuildLive(camera, StreamPurpose.Main);
            if (string.IsNullOrWhiteSpace(url))
                return;

            var subscriber = new Subscriber(maxWidth);
            var broadcaster = Rent(camera.Id, url!, subscriber);
            if (broadcaster == null)
            {
                _logger.LogWarning("Kamera #{Id} broadcaster'i ishga tushmadi", camera.Id);
                return;
            }

            try
            {
                await foreach (var jpeg in subscriber.Queue.Reader.ReadAllAsync(ct))
                {
                    // multipart bo'lak: --frame\r\nContent-Type...\r\n\r\n<bytes>\r\n
                    var header = $"--{Boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n";
                    await output.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
                    await output.WriteAsync(jpeg, ct);
                    await output.WriteAsync("\r\n"u8.ToArray(), ct);
                    await output.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { /* mijoz uzildi — normal */ }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Kamera #{Id} MJPEG yozishda uzilish", camera.Id);
            }
            finally
            {
                Release(camera.Id, broadcaster, subscriber);
            }
        }

        // ===== Bitta kadr =====
        public async Task<byte[]?> SnapshotAsync(Camera camera, int? maxWidth, CancellationToken ct)
        {
            // Ko'rsatish uchun MAIN stream afzal (StreamUrl -> AiStreamUrl -> vendor shabloni).
            var url = _urlBuilder.BuildLive(camera, StreamPurpose.Main);
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // Snapshot HAM broadcaster orqali olinadi (ilgari alohida VideoCapture ochilib,
            // 30 tagacha kadr o'qilardi — bu 1-3 soniya edi):
            //   - kamera allaqachon efirda bo'lsa (grid jonli MJPEG ishlatadi) — YANGI RTSP
            //     ulanish umuman ochilmaydi, mavjud capture'dan keyingi kadr bir necha o'n ms da keladi;
            //   - efirda bo'lmasa — broadcaster ishga tushadi va biz BIRINCHI YAROQLI kadrni kutamiz;
            //     ulanish narxi bir marta to'lanadi va shu paytda grid ochilsa capture qayta ishlatiladi.
            var subscriber = new Subscriber(maxWidth);
            var broadcaster = Rent(camera.Id, url!, subscriber);
            if (broadcaster == null)
            {
                _logger.LogWarning("Kamera #{Id} snapshot uchun broadcaster ishga tushmadi", camera.Id);
                return null;
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(_snapshotTimeoutSeconds));
                return await subscriber.Queue.Reader.ReadAsync(timeout.Token);
            }
            catch (OperationCanceledException) { return null; }   // timeout yoki mijoz uzildi
            catch (ChannelClosedException) { return null; }       // broadcaster to'xtadi (ulanmadi)
            finally
            {
                Release(camera.Id, broadcaster, subscriber);
            }
        }

        // ===== Broadcaster ref-counting =====
        private Broadcaster? Rent(int cameraId, string url, Subscriber subscriber)
        {
            if (_disposed) return null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                var broadcaster = _broadcasters.GetOrAdd(
                    cameraId, _ => new Broadcaster(cameraId, url, _targetFps, _maxDrainFrames, _logger));

                if (broadcaster.TryAddSubscriber(subscriber))
                    return broadcaster;

                // To'xtagan broadcaster — lug'atdan olib tashlab qayta urinamiz.
                _broadcasters.TryRemove(new KeyValuePair<int, Broadcaster>(cameraId, broadcaster));
            }

            return null;
        }

        private void Release(int cameraId, Broadcaster broadcaster, Subscriber subscriber)
        {
            if (broadcaster.RemoveSubscriber(subscriber) || broadcaster.IsStopped)
            {
                // Oxirgi mijoz ketdi — capture yopiladi va lug'atdan olib tashlanadi.
                _broadcasters.TryRemove(new KeyValuePair<int, Broadcaster>(cameraId, broadcaster));
                broadcaster.Dispose();
                _logger.LogDebug("Kamera #{Id} ko'rsatish oqimi yopildi (mijoz qolmadi)", cameraId);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var pair in _broadcasters.ToArray())
            {
                _broadcasters.TryRemove(pair.Key, out _);
                pair.Value.Dispose();
            }
        }

        // ===== JPEG kodlash (kerak bo'lsa kichraytirib) =====
        private static byte[] EncodeJpeg(Mat frame, int? maxWidth)
        {
            var prm = new ImageEncodingParam(ImwriteFlags.JpegQuality, JpegQuality);

            if (maxWidth.HasValue && maxWidth.Value > 0 && frame.Width > maxWidth.Value)
            {
                var scale = maxWidth.Value / (double)frame.Width;
                using var resized = new Mat();
                Cv2.Resize(frame, resized, new Size(maxWidth.Value, Math.Max(1, (int)(frame.Height * scale))));
                return resized.ImEncode(".jpg", prm);
            }

            return frame.ImEncode(".jpg", prm);
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

        // ===== Ichki turlar =====

        /// <summary>Bitta mijozning navbati. Capacity 1 + DropOldest — sekin mijoz kadr tashlaydi.</summary>
        private sealed class Subscriber
        {
            public int? MaxWidth { get; }
            public Channel<byte[]> Queue { get; }

            public Subscriber(int? maxWidth)
            {
                MaxWidth = maxWidth;
                Queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true
                });
            }
        }

        /// <summary>
        /// Bitta kamera uchun bitta VideoCapture + bitta producer thread.
        /// Kadrlarni barcha obunachilarga fan-out qiladi (bir xil maxWidth uchun JPEG bir marta kodlanadi).
        /// </summary>
        private sealed class Broadcaster : IDisposable
        {
            /// <summary>Ketma-ket bo'sh o'qishlar shundan oshsa — oqim uzilgan deb hisoblaymiz.</summary>
            private const int MaxEmptyReads = 50;

            /// <summary>Bo'sh o'qishda progressiv kutish qadami (ms) va yuqori chegarasi.</summary>
            private const int EmptyWaitStepMs = 10;
            private const int MaxEmptyWaitMs = 200;

            /// <summary>
            /// <c>Grab()</c> shundan tez qaytsa — demak kadr FFMPEG navbatida TAYYOR turgan edi,
            /// ya'ni u ESKI kadr. Sekin qaytsa — tarmoqni kutdik, ya'ni bu real vaqt kadri.
            /// </summary>
            private const double FastGrabThresholdMs = 5.0;

            private readonly int _cameraId;
            private readonly string _url;
            private readonly int _targetFps;
            private readonly int _maxDrainFrames;
            private readonly ILogger _logger;
            private readonly CancellationTokenSource _cts = new();
            private readonly object _sync = new();
            private readonly List<Subscriber> _subscribers = new();

            private Task? _producer;
            private bool _stopped;
            private int _disposed;

            /// <summary>Producer to'xtagan bo'lsa true — bunday broadcaster qayta ishlatilmaydi.</summary>
            public bool IsStopped
            {
                get { lock (_sync) { return _stopped; } }
            }

            public Broadcaster(int cameraId, string url, int targetFps, int maxDrainFrames, ILogger logger)
            {
                _cameraId = cameraId;
                _url = url;
                _targetFps = targetFps;
                _maxDrainFrames = maxDrainFrames;
                _logger = logger;
            }

            /// <summary>Obunachi qo'shadi. Broadcaster to'xtagan bo'lsa false qaytaradi.</summary>
            public bool TryAddSubscriber(Subscriber subscriber)
            {
                lock (_sync)
                {
                    if (_stopped) return false;
                    _subscribers.Add(subscriber);

                    _producer ??= Task.Factory.StartNew(
                        ProducerLoop, CancellationToken.None,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    return true;
                }
            }

            /// <summary>Obunachini olib tashlaydi. Oxirgisi bo'lsa true qaytaradi (capture yopiladi).</summary>
            public bool RemoveSubscriber(Subscriber subscriber)
            {
                lock (_sync)
                {
                    _subscribers.Remove(subscriber);
                    subscriber.Queue.Writer.TryComplete();

                    if (_subscribers.Count > 0 || _stopped) return false;
                    _stopped = true;
                    return true;
                }
            }

            /// <summary>
            /// FFMPEG demuxer navbatida to'planib qolgan ESKI kadrlarni tashlab yuborib,
            /// eng yangisini "grab" qiladi. <c>Retrieve()</c> shundan keyin chaqiriladi.
            ///
            /// <para><b>Nega <c>while (capture.Grab()) { }</c> EMAS:</b> u kamera bizdan tez kadr
            /// yuborsa hech qachon tugamaydi va CPU'ni 100% band qiladi (hech qachon kadr ko'rsatilmaydi).
            /// Shuning uchun ikkita mustaqil to'xtash sharti bor:</para>
            /// <list type="number">
            ///   <item><b>Qattiq chegara:</b> ko'pi bilan <c>_maxDrainFrames</c> marta <c>Grab()</c>
            ///   (default 4). Ya'ni bitta iteratsiya har doim chekli.</item>
            ///   <item><b>Moslashuvchan chegara:</b> <c>Grab()</c> <see cref="FastGrabThresholdMs"/> dan
            ///   sekin qaytsa — navbat bo'sh, biz tarmoqni kutdik, demak bu kadr ALLAQACHON eng yangisi.
            ///   Darhol to'xtaymiz.</item>
            /// </list>
            ///
            /// <para>Natijada barqaror holatda (kechikish yo'q) iteratsiyaga 1 ta <c>Grab()</c> to'g'ri keladi
            /// — kadr yo'qotilmaydi, to'liq FPS. Orqada qolgan holatda esa navbat 4x tezlikda bo'shatiladi
            /// va bir necha iteratsiyada real vaqtga yetib olamiz. Sikl har doim tarmoqni kutishda
            /// bloklanadi — busy-loop bo'lishi mumkin emas.</para>
            /// </summary>
            private bool GrabLatest(VideoCapture capture)
            {
                var grabbed = false;

                for (var i = 0; i < _maxDrainFrames; i++)
                {
                    var startedAt = Stopwatch.GetTimestamp();
                    if (!capture.Grab()) break;   // oqim uzildi / kadr yo'q
                    grabbed = true;

                    var elapsedMs = (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;
                    if (elapsedMs >= FastGrabThresholdMs) break;   // navbat bo'sh edi — bu kadr yangi
                }

                return grabbed;
            }

            private void ProducerLoop()
            {
                var ct = _cts.Token;

                // TargetFps == 0 → cheklov yo'q. Aks holda kadrlar UXLAMASDAN, shunchaki
                // tashlab yuborish orqali cheklanadi (uxlash navbat to'planishiga olib kelardi).
                var frameIntervalMs = _targetFps > 0 ? Math.Max(1, 1000 / _targetFps) : 0;

                try
                {
                    using var capture = new VideoCapture(_url);

                    // DIQQAT: CAP_PROP_BUFFERSIZE OpenCV'ning FFMPEG backend'ida (RTSP/HTTP oqimlar)
                    // UMUMAN QO'LLANMAYDI — u faqat DSHOW / V4L2 / GStreamer backend'larida ishlaydi.
                    // Ya'ni bu chaqiruv RTSP uchun HECH NARSA qilmaydi va unga TAYANIB BO'LMAYDI.
                    // Kechikishga qarshi asosiy himoya — yuqoridagi GrabLatest() drain mantig'i.
                    // O'chirilmadi: boshqa backend (masalan lokal USB kamera) ishlatilsa foydali.
                    capture.Set(VideoCaptureProperties.BufferSize, 1);

                    if (!capture.IsOpened())
                    {
                        _logger.LogWarning("Kamera #{Id} ko'rsatish uchun ochilmadi: {Url}",
                            _cameraId, MaskUrl(_url));
                        return;
                    }

                    using var frame = new Mat();
                    var emptyReads = 0;
                    var encoded = new Dictionary<int, byte[]>();
                    long lastDeliveredAtMs = 0;

                    while (!ct.IsCancellationRequested)
                    {
                        // Navbatni bo'shatib eng yangi kadrni olamiz (Grab x N + Retrieve).
                        // Retrieve natijasi ataylab tekshirilmaydi — kadr yaroqliligini
                        // frame.Empty() aniq ko'rsatadi (backend'ga bog'liq emas).
                        var grabbed = GrabLatest(capture);
                        if (grabbed) capture.Retrieve(frame, 0);

                        if (!grabbed || frame.Empty())
                        {
                            if (++emptyReads > MaxEmptyReads) break;   // oqim uzildi

                            // Progressiv kutish: birinchi bo'sh o'qishlarda tez qayta urinamiz
                            // (uzilishdan keyin tez tiklanish), ketma-ket ko'p bo'lsa sekinlashamiz
                            // (ulanmagan kamerada CPU'ni bo'sh aylantirmaslik uchun).
                            var waitMs = Math.Min(MaxEmptyWaitMs, EmptyWaitStepMs * emptyReads);
                            if (ct.WaitHandle.WaitOne(waitMs)) break;
                            continue;
                        }
                        emptyReads = 0;

                        Subscriber[] current;
                        lock (_sync) { current = _subscribers.ToArray(); }
                        if (current.Length == 0)
                        {
                            // Mijoz yo'q — UXLAMAYMIZ. Uxlasak navbat yana to'planardi;
                            // Grab() o'zi tarmoqni kutib turadi, shuning uchun busy-loop yo'q.
                            // (Bu holat vaqtinchalik: oxirgi mijoz ketsa broadcaster butunlay to'xtaydi.)
                            continue;
                        }

                        // FPS chegarasi: kadrni TASHLAYMIZ, lekin siklni to'xtatmaymiz —
                        // shunda FFMPEG navbati bo'sh qoladi va kechikish o'smaydi.
                        if (frameIntervalMs > 0)
                        {
                            var nowMs = Environment.TickCount64;
                            if (nowMs - lastDeliveredAtMs < frameIntervalMs) continue;
                            lastDeliveredAtMs = nowMs;
                        }

                        encoded.Clear();
                        foreach (var sub in current)
                        {
                            var key = sub.MaxWidth ?? 0;
                            if (!encoded.TryGetValue(key, out var jpeg))
                            {
                                // Bir xil maxWidth'li mijozlar uchun JPEG BIR MARTA kodlanadi —
                                // grid'da 9-16 mijoz bo'lsa ham CPU narxi bitta kodlashga teng.
                                jpeg = EncodeJpeg(frame, sub.MaxWidth);
                                encoded[key] = jpeg;
                            }
                            // TryWrite bloklamaydi: navbat to'la bo'lsa DropOldest eski kadrni tashlaydi.
                            // Sekin mijoz boshqa mijozlarni ham, producer'ni ham sekinlashtirmaydi.
                            sub.Queue.Writer.TryWrite(jpeg);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kamera #{Id} ko'rsatish oqimida xato", _cameraId);
                }
                finally
                {
                    // Mijozlar `await foreach` dan chiqib ketishi uchun navbatlarni yopamiz.
                    lock (_sync)
                    {
                        _stopped = true;
                        foreach (var sub in _subscribers) sub.Queue.Writer.TryComplete();
                        _subscribers.Clear();
                    }
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

                Task? producer;
                lock (_sync)
                {
                    _stopped = true;
                    producer = _producer;
                }

                try { _cts.Cancel(); } catch { }

                // CTS'ni producer thread hali ishlatayotgan bo'lishi mumkin —
                // shuning uchun uni faqat producer tugagandan keyin dispose qilamiz.
                if (producer != null)
                    producer.ContinueWith(_ => { try { _cts.Dispose(); } catch { } }, TaskScheduler.Default);
                else
                    _cts.Dispose();
            }
        }
    }
}
