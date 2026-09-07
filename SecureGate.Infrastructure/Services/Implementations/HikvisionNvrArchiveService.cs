using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations;

/// <summary>
/// Hikvision NVR/DVR arxivini ISAPI (HTTP) va RTSP playback orqali o'qiydi.
///
/// <para><b>Qidiruv:</b> <c>POST http://{host}:{isapiPort}/ISAPI/ContentMgmt/search</c> —
/// <c>CMSearchDescription</c> XML yuboriladi, javobda <c>CMSearchResult</c> keladi.
/// Sahifalash <c>searchResultPostion</c> (Hikvision hujjatidagi imlo xatosi — shundayligicha) orqali.</para>
///
/// <para><b>Playback:</b> <c>rtsp://{host}:{rtspPort}/Streaming/tracks/{ch}01?starttime=...&amp;endtime=...</c>
/// RTSP oqimi <c>ffmpeg -c copy</c> bilan fragmentli mp4 ga o'raladi (qayta kodlash yo'q).</para>
///
/// <para><b>Autentifikatsiya:</b> Hikvision odatda HTTP Digest talab qiladi; Basic fallback sifatida sinaladi.</para>
///
/// <para>Holatsiz — <b>Singleton</b>.</para>
/// </summary>
public sealed class HikvisionNvrArchiveService : INvrArchiveService
{
    /// <summary>Program.cs da <c>AddHttpClient(HikvisionNvrArchiveService.HttpClientName)</c> deb ro'yxatga olinadi.</summary>
    public const string HttpClientName = "HikvisionIsapi";

    // Hikvision ISAPI javoblarida ishlatiladigan nom fazolari har xil bo'lishi mumkin
    // (ver10/ver20/bo'sh) — shu sababli elementlar LocalName bo'yicha topiladi.
    private const int MaxResultsPerPage = 100;
    private const int MaxPages = 50;          // 50 x 100 = 5000 segment — himoya chegarasi
    private const int StdErrTailLines = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICameraCredentialProtector _cred;
    private readonly IStreamUrlBuilder _urlBuilder;
    private readonly ILogger<HikvisionNvrArchiveService> _logger;

    private readonly int _isapiPort;
    private readonly string _ffmpegPath;
    private readonly TimeSpan _searchTimeout;
    private readonly TimeSpan _playbackStartTimeout;

    public HikvisionNvrArchiveService(
        IHttpClientFactory httpClientFactory,
        ICameraCredentialProtector cred,
        IStreamUrlBuilder urlBuilder,
        IConfiguration config,
        ILogger<HikvisionNvrArchiveService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cred = cred;
        _urlBuilder = urlBuilder;
        _logger = logger;

        // Camera entity'sida ISAPI porti uchun alohida maydon yo'q: RTSP 554, ISAPI esa HTTP 80.
        _isapiPort = config.GetValue<int?>("Nvr:IsapiPort") ?? 80;
        if (_isapiPort is < 1 or > 65535) _isapiPort = 80;

        var ffmpegPath = config.GetValue<string?>("Nvr:FfmpegPath");
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath.Trim();

        _searchTimeout = TimeSpan.FromSeconds(config.GetValue<int?>("Nvr:SearchTimeoutSeconds") ?? 15);
        _playbackStartTimeout = TimeSpan.FromSeconds(config.GetValue<int?>("Nvr:PlaybackStartTimeoutSeconds") ?? 20);
    }

    // ===== Supports =====

    public bool Supports(Camera camera)
    {
        if (camera is null) return false;
        if (camera.CameraModel != CameraModel.Hikvision) return false;
        if (camera.DeviceKind != DeviceKind.NvrChannel && !camera.ChannelNumber.HasValue) return false;
        return !string.IsNullOrWhiteSpace(camera.IpAddress);
    }

    // ===== Qidiruv =====

    public async Task<IReadOnlyList<ArchiveSegment>> SearchAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        EnsureSupported(camera);
        EnsureRange(fromUtc, toUtc);

        var trackId = BuildTrackId(camera);
        var endpoint = new Uri($"http://{camera.IpAddress!.Trim()}:{_isapiPort.ToString(CultureInfo.InvariantCulture)}/ISAPI/ContentMgmt/search");

        var (username, password) = GetCredentials(camera);
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var segments = new List<ArchiveSegment>();
        var position = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            var body = BuildSearchXml(trackId, fromUtc, toUtc, position);

            string xml;
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutCts.CancelAfter(_searchTimeout);
                try
                {
                    using var response = await SendWithAuthAsync(
                        client,
                        () => new HttpRequestMessage(HttpMethod.Post, endpoint)
                        {
                            Content = new StringContent(body, Encoding.UTF8, "application/xml")
                        },
                        username, password, timeoutCts.Token).ConfigureAwait(false);

                    xml = await ReadAndValidateAsync(response, camera, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Kamera #{Id}: NVR arxiv qidiruvi {Sec}s ichida javob bermadi ({Host})",
                        camera.Id, _searchTimeout.TotalSeconds, camera.IpAddress);
                    throw new InvalidOperationException(
                        $"NVR ({camera.IpAddress}) arxiv qidiruviga {_searchTimeout.TotalSeconds:0} soniya ichida javob bermadi.");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Kamera #{Id}: NVR ISAPI ga ulanib bo'lmadi ({Host}:{Port})",
                        camera.Id, camera.IpAddress, _isapiPort);
                    throw new InvalidOperationException(
                        $"NVR ({camera.IpAddress}:{_isapiPort}) bilan bog'lanib bo'lmadi. IP manzil, ISAPI porti va tarmoqni tekshiring.", ex);
                }
            }

            var (pageSegments, hasMore, matched) = ParseSearchResult(xml, camera);
            segments.AddRange(pageSegments);

            if (!hasMore || matched <= 0) break;
            position += matched;
        }

        _logger.LogDebug("Kamera #{Id}: arxivda {Count} segment topildi ({From:u} — {To:u})",
            camera.Id, segments.Count, fromUtc, toUtc);

        return segments;
    }

    // ===== Playback =====

    public async Task<Stream> OpenPlaybackAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        EnsureSupported(camera);
        EnsureRange(fromUtc, toUtc);

        var trackId = BuildTrackId(camera);
        var path = $"/Streaming/tracks/{trackId}?starttime={FormatRtspTime(fromUtc)}&endtime={FormatRtspTime(toUtc)}";

        var rtspUrl = _urlBuilder.BuildWithPath(camera, path)
            ?? throw new InvalidOperationException("NVR uchun playback URL yasab bo'lmadi — IP manzil to'ldirilmagan.");

        return await StartFfmpegAsync(camera, rtspUrl, ct).ConfigureAwait(false);
    }

    private async Task<Stream> StartFfmpegAsync(Camera camera, string rtspUrl, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList — qo'shtirnoq/probel muammolarisiz (URL'da parol bo'lishi mumkin).
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");
        psi.ArgumentList.Add("-rtsp_transport");
        psi.ArgumentList.Add("tcp");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(rtspUrl);
        // -c copy: qayta kodlash yo'q (CPU tejaladi).
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        // frag_keyframe+empty_moov: oqimli (seek qilib bo'lmaydigan) mp4 uchun SHART —
        // oddiy mp4 moov atomini fayl oxiriga yozadi, pipe'da esa bu ishlamaydi.
        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("frag_keyframe+empty_moov");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("mp4");
        psi.ArgumentList.Add("pipe:1");

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // stderr'ni O'QISH SHART: aks holda pipe buferi to'ladi va ffmpeg osilib qoladi.
        var stderrTail = new Queue<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lock (stderrTail)
            {
                stderrTail.Enqueue(e.Data);
                while (stderrTail.Count > StdErrTailLines) stderrTail.Dequeue();
            }
            _logger.LogDebug("ffmpeg (kamera #{Id}): {Line}", camera.Id, RedactCredentials(e.Data));
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            _logger.LogError(ex, "ffmpeg ishga tushmadi (yo'l: {Path})", _ffmpegPath);
            throw new InvalidOperationException(
                $"ffmpeg ishga tushmadi ('{_ffmpegPath}'). Server'da ffmpeg o'rnatilganini yoki 'Nvr:FfmpegPath' sozlamasini tekshiring.", ex);
        }

        process.BeginErrorReadLine();

        var stdout = process.StandardOutput.BaseStream;

        // Playback haqiqatdan boshlanganini tekshiramiz: birinchi baytlarni kutamiz.
        // O'qilgan baytlar yo'qolmasligi uchun ular wrapper'ga "prefiks" bo'lib beriladi.
        var buffer = new byte[64 * 1024];
        var readTask = stdout.ReadAsync(buffer, 0, buffer.Length, ct);

        // Delay uchun alohida CTS — birinchi baytlar kelgach taymer darhol bekor qilinadi
        // (aks holda har bir so'rov ortidan 20s "osilib turgan" taymer qolardi).
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var finished = await Task.WhenAny(readTask, Task.Delay(_playbackStartTimeout, delayCts.Token))
            .ConfigureAwait(false);
        delayCts.Cancel();

        if (finished != readTask)
        {
            // Mijoz so'rovni uzgan bo'lsa — bu timeout emas, oddiy bekor qilish.
            if (ct.IsCancellationRequested)
            {
                Observe(readTask);
                KillQuietly(process);
                ct.ThrowIfCancellationRequested();
            }

            // Kutish tugadi — osilib qolmaslik uchun process o'ldiriladi.
            Observe(readTask);
            KillQuietly(process);
            var tail = StdErrTail(stderrTail);
            _logger.LogWarning("Kamera #{Id}: NVR playback {Sec}s ichida boshlanmadi. ffmpeg: {Tail}",
                camera.Id, _playbackStartTimeout.TotalSeconds, tail);
            throw new InvalidOperationException(
                $"NVR arxiv oqimi {_playbackStartTimeout.TotalSeconds:0} soniya ichida boshlanmadi. " +
                "Tanlangan vaqt oralig'ida yozuv bo'lmasligi yoki NVR band bo'lishi mumkin.");
        }

        int read;
        try
        {
            read = await readTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            KillQuietly(process);
            throw new InvalidOperationException("NVR arxiv oqimini o'qishda xato yuz berdi.", ex);
        }

        if (read <= 0)
        {
            // ffmpeg hech narsa bermay tugadi — odatda RTSP xatosi (401, yozuv yo'q, kanal noto'g'ri).
            KillQuietly(process);
            var tail = StdErrTail(stderrTail);
            _logger.LogWarning("Kamera #{Id}: ffmpeg oqim bermadi. URL: {Url}. ffmpeg: {Tail}",
                camera.Id, _urlBuilder.Mask(rtspUrl), tail);
            throw new InvalidOperationException(
                "NVR arxividan video olinmadi. Login/parol, kanal raqami yoki tanlangan vaqt oralig'ini tekshiring." +
                (string.IsNullOrEmpty(tail) ? "" : $" (ffmpeg: {tail})"));
        }

        _logger.LogInformation("Kamera #{Id}: NVR arxiv oqimi boshlandi ({Url})",
            camera.Id, _urlBuilder.Mask(rtspUrl));

        return new FfmpegProcessStream(process, stdout, buffer, read, _logger);
    }

    // ===== ISAPI so'rovi + autentifikatsiya =====

    /// <summary>
    /// So'rovni yuboradi; 401 kelsa Digest (bo'lmasa Basic) challenge'ga javob berib qayta yuboradi.
    /// <paramref name="requestFactory"/> har urinishda YANGI <see cref="HttpRequestMessage"/> yasashi shart —
    /// bir marta yuborilgan so'rovni qayta ishlatib bo'lmaydi (content oqimi tugagan bo'ladi).
    /// </summary>
    private static async Task<HttpResponseMessage> SendWithAuthAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        string username,
        string password,
        CancellationToken ct)
    {
        var first = await client.SendAsync(requestFactory(), HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (first.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(username))
            return first;

        // 1) Digest (Hikvision default)
        var digestChallenge = HttpDigestAuth.FindChallenge(first, "Digest");
        if (digestChallenge?.Parameter is { Length: > 0 } challenge)
        {
            var request = requestFactory();
            var header = HttpDigestAuth.BuildDigestHeader(
                challenge, request.Method.Method, request.RequestUri!, username, password);

            if (header is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Digest", header);
                first.Dispose();

                var digestResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                if (digestResponse.StatusCode != HttpStatusCode.Unauthorized)
                    return digestResponse;

                first = digestResponse; // Basic fallback'ga o'tamiz
            }
            else
            {
                request.Dispose();
            }
        }

        // 2) Basic fallback (eski/soddalashtirilgan firmware'lar)
        var basicRequest = requestFactory();
        basicRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", HttpDigestAuth.BuildBasicHeader(username, password));

        first.Dispose();
        return await client.SendAsync(basicRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }

    private async Task<string> ReadAndValidateAsync(HttpResponseMessage response, Camera camera, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Kamera #{Id}: NVR ISAPI 401 — login/parol noto'g'ri ({Host})", camera.Id, camera.IpAddress);
            throw new InvalidOperationException(
                $"NVR ({camera.IpAddress}) login yoki parolni qabul qilmadi (401). Kamera sozlamalaridagi login/parolni tekshiring.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Kamera #{Id}: NVR ISAPI {Status} qaytardi ({Host})",
                camera.Id, (int)response.StatusCode, camera.IpAddress);
            throw new InvalidOperationException(
                $"NVR ({camera.IpAddress}) arxiv qidiruviga {(int)response.StatusCode} xato kodi bilan javob berdi.");
        }

        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    // ===== XML =====

    private static string BuildSearchXml(string trackId, DateTime fromUtc, DateTime toUtc, int position)
    {
        var searchId = Guid.NewGuid().ToString().ToUpperInvariant();

        // DIQQAT: "searchResultPostion" — Hikvision hujjatidagi imlo xatosi, firmware aynan shuni kutadi.
        return $"""
                <?xml version="1.0" encoding="utf-8"?>
                <CMSearchDescription>
                  <searchID>{searchId}</searchID>
                  <trackIDList>
                    <trackID>{trackId}</trackID>
                  </trackIDList>
                  <timeSpanList>
                    <timeSpan>
                      <startTime>{FormatIsapiTime(fromUtc)}</startTime>
                      <endTime>{FormatIsapiTime(toUtc)}</endTime>
                    </timeSpan>
                  </timeSpanList>
                  <maxResults>{MaxResultsPerPage.ToString(CultureInfo.InvariantCulture)}</maxResults>
                  <searchResultPostion>{position.ToString(CultureInfo.InvariantCulture)}</searchResultPostion>
                  <metadataList>
                    <metadataDescriptor>//recordType.meta.std-cgi.com</metadataDescriptor>
                  </metadataList>
                </CMSearchDescription>
                """;
    }

    private (List<ArchiveSegment> Segments, bool HasMore, int Matched) ParseSearchResult(string xml, Camera camera)
    {
        var segments = new List<ArchiveSegment>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kamera #{Id}: NVR javobi XML sifatida o'qilmadi", camera.Id);
            throw new InvalidOperationException("NVR tushunarsiz javob qaytardi (XML o'qilmadi).", ex);
        }

        var root = doc.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "CMSearchResult", StringComparison.OrdinalIgnoreCase))
        {
            // Qurilma ko'pincha xatoni <ResponseStatus> ichida qaytaradi.
            var description = Local(root, "statusString") ?? Local(root, "subStatusCode") ?? root?.Name.LocalName;
            throw new InvalidOperationException($"NVR arxiv qidiruvini rad etdi: {description ?? "noma'lum javob"}.");
        }

        var responseStatus = Local(root, "responseStatus");
        if (string.Equals(responseStatus, "false", StringComparison.OrdinalIgnoreCase))
        {
            var description = Local(root, "responseStatusStrig") ?? Local(root, "responseStatusString");
            throw new InvalidOperationException($"NVR arxiv qidiruvini bajara olmadi: {description ?? "noma'lum sabab"}.");
        }

        foreach (var item in root.Descendants().Where(e => e.Name.LocalName == "searchMatchItem"))
        {
            var timeSpan = item.Elements().FirstOrDefault(e => e.Name.LocalName == "timeSpan");
            if (timeSpan is null) continue;

            if (!TryParseUtc(Local(timeSpan, "startTime"), out var start)) continue;
            if (!TryParseUtc(Local(timeSpan, "endTime"), out var end)) continue;

            var descriptor = item.Elements().FirstOrDefault(e => e.Name.LocalName == "mediaSegmentDescriptor");
            var playbackUri = descriptor is null ? null : Local(descriptor, "playbackURI");

            segments.Add(new ArchiveSegment(
                start,
                end,
                TryParseSizeFromPlaybackUri(playbackUri),
                Local(item, "trackID")));
        }

        var matchCount = root.Descendants().Count(e => e.Name.LocalName == "searchMatchItem");

        // "MORE" — natijalar tugamagan, keyingi sahifani so'raymiz.
        var statusText = Local(root, "responseStatusStrig") ?? Local(root, "responseStatusString");
        var hasMore = string.Equals(statusText, "MORE", StringComparison.OrdinalIgnoreCase);

        return (segments, hasMore, matchCount);
    }

    // ===== Yordamchilar =====

    private void EnsureSupported(Camera camera)
    {
        if (!Supports(camera))
            throw new InvalidOperationException(
                $"'{camera?.Name}' kamerasi Hikvision NVR arxivi uchun mos emas. " +
                "Model 'Hikvision', qurilma turi 'NVR kanali', kanal raqami va IP manzil to'ldirilgan bo'lishi kerak.");
    }

    private static void EnsureRange(DateTime fromUtc, DateTime toUtc)
    {
        if (toUtc <= fromUtc)
            throw new InvalidOperationException("Tugash vaqti boshlanish vaqtidan keyin bo'lishi kerak.");
    }

    /// <summary>Hikvision track raqami: kanal 1 → "101", kanal 3 → "301" (main stream track'i).</summary>
    private static string BuildTrackId(Camera camera)
    {
        var channel = camera.ChannelNumber is > 0 ? camera.ChannelNumber.Value : 1;
        return (channel * 100 + 1).ToString(CultureInfo.InvariantCulture);
    }

    private (string Username, string Password) GetCredentials(Camera camera) =>
        (camera.Username ?? string.Empty, _cred.Unprotect(camera.Password) ?? string.Empty);

    /// <summary>ISAPI XML vaqt formati: 2026-09-07T10:00:00Z</summary>
    private static string FormatIsapiTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>RTSP playback vaqt formati: 20260907T100000Z</summary>
    private static string FormatRtspTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string? Local(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();

    private static bool TryParseUtc(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            utc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    /// <summary>
    /// playbackURI ichida ba'zi firmware'lar <c>&amp;size=12345</c> beradi — bo'lsa hajmni olamiz.
    /// Bo'lmasa null (interfeys buni ruxsat etadi).
    /// </summary>
    private static long? TryParseSizeFromPlaybackUri(string? playbackUri)
    {
        if (string.IsNullOrWhiteSpace(playbackUri)) return null;

        var idx = playbackUri.IndexOf("size=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var start = idx + "size=".Length;
        var end = start;
        while (end < playbackUri.Length && char.IsDigit(playbackUri[end])) end++;

        if (end == start) return null;
        return long.TryParse(playbackUri.AsSpan(start, end - start), NumberStyles.None,
            CultureInfo.InvariantCulture, out var size) ? size : null;
    }

    private static void Observe(Task task) =>
        _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

    private void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ffmpeg process'ini to'xtatishda xato");
        }
        finally
        {
            try { process.Dispose(); } catch { /* ignore */ }
        }
    }

    private static string StdErrTail(Queue<string> tail)
    {
        lock (tail)
        {
            // DIQQAT: ffmpeg stderr'i kirish URL'ini aks ettiradi
            // ("Input #0, rtsp, from 'rtsp://admin:parol@host/...'"), va bu matn
            // istisno xabari orqali HTTP javobiga chiqadi. Shuning uchun
            // qaytarishdan OLDIN kirish ma'lumotlari tozalanadi.
            return tail.Count == 0
                ? string.Empty
                : RedactCredentials(string.Join(" | ", tail));
        }
    }

    /// <summary>
    /// Ixtiyoriy matndagi <c>scheme://login:parol@host</c> ko'rinishidagi kirish
    /// ma'lumotlarini yashiradi. ffmpeg/kutubxona chiqishini log yoki HTTP javobiga
    /// uzatishdan oldin qo'llanadi — NVR paroli tashqariga chiqmasligi uchun.
    /// </summary>
    private static string RedactCredentials(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        try
        {
            return CredentialsInUrl.Replace(text, "$1://***:***@");
        }
        catch (RegexMatchTimeoutException)
        {
            // Regex to'xtab qolsa — xom matnni QAYTARMAYMIZ (parol chiqib ketmasin).
            return "(chiqish yashirildi)";
        }
    }

    private static readonly Regex CredentialsInUrl = new(
        // userinfo qismi ochko'z (greedy) olinadi va bo'shliq/`/` bilan chegaralanadi —
        // shunda parol ichida `@` bo'lsa ham oxirgi `@` gacha to'liq yashiriladi.
        @"([a-zA-Z][a-zA-Z0-9+.\-]*)://[^\s/]*:[^\s/]*@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));
}
