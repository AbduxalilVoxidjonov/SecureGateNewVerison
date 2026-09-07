namespace SecureGate.Infrastructure.Services.Implementations
{
    /// <summary>
    /// OpenCV/FFMPEG uchun process-global sozlamalarni BIR MARTA o'rnatadi.
    ///
    /// <para>
    /// Ilgari <c>OPENCV_FFMPEG_CAPTURE_OPTIONS</c> muhit o'zgaruvchisi har bir
    /// <c>VideoCapture</c> ochilishidan oldin qayta yozilardi — bu process-global
    /// holat bo'lgani uchun parallel oqimlar orasida poyga (race) yuzaga kelardi.
    /// Endi u faqat bir marta, ilova ishga tushganda o'rnatiladi.
    /// </para>
    ///
    /// <para>
    /// Chaqirilishi kerak: <c>Program.cs</c> boshida (host qurilishidan oldin),
    /// har qanday <c>VideoCapture</c> yaratilishidan avval:
    /// <c>OpenCvBootstrap.Configure();</c>
    /// yoki konfiguratsiyadan o'qish uchun: <c>OpenCvBootstrap.Configure(builder.Configuration);</c>
    /// </para>
    ///
    /// <para>
    /// Ustuvorlik (yuqoridan pastga):
    /// <list type="number">
    ///   <item><c>OPENCV_FFMPEG_CAPTURE_OPTIONS</c> muhit o'zgaruvchisi (Docker/operator bergan bo'lsa);</item>
    ///   <item><c>Camera:FfmpegOptions</c> konfiguratsiya kaliti (agar <c>IConfiguration</c> berilgan bo'lsa);</item>
    ///   <item><see cref="DefaultCaptureOptions"/> — past kechikishga sozlangan default.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class OpenCvBootstrap
    {
        private const string CaptureOptionsVariable = "OPENCV_FFMPEG_CAPTURE_OPTIONS";

        /// <summary>Konfiguratsiyadan o'qiladigan kalit (ixtiyoriy override).</summary>
        public const string ConfigKey = "Camera:FfmpegOptions";

        /// <summary>
        /// RTSP uchun PAST KECHIKISH (low-latency) sozlamalari. FFMPEG buferlashi
        /// jonli oqimda kechikishning asosiy manbalaridan biri.
        ///
        /// <list type="bullet">
        ///   <item><c>rtsp_transport;tcp</c> — RTSP'ni TCP orqali (UDP'da kadr yo'qolishi/tartibsizligi bo'ladi).</item>
        ///   <item><c>stimeout;5000000</c> — socket timeout 5 s (mikrosekundda). Kamera javob bermasa
        ///     <c>VideoCapture</c> cheksiz osilib qolmaydi.</item>
        ///   <item><c>fflags;nobuffer</c> — demuxer kirish buferini to'ldirib o'tirmaydi, paketni
        ///     kelishi bilan beradi. Kechikishga eng ko'p ta'sir qiladigan flag.</item>
        ///   <item><c>flags;low_delay</c> — dekoderga kadrni kechiktirmasdan (B-frame kutmasdan)
        ///     chiqarishni buyuradi.</item>
        ///   <item><c>max_delay;500000</c> — demuxer paketlarni qayta tartiblash uchun ko'pi bilan
        ///     0.5 s kutadi (default 5 s edi — ya'ni 5 s kechikish manbai).</item>
        ///   <item><c>reorder_queue_size;0</c> — RTP paketlarini qayta tartiblash navbati o'chirilgan;
        ///     TCP transportda tartib allaqachon kafolatlangan, navbat faqat kechikish qo'shadi.</item>
        /// </list>
        /// </summary>
        private const string DefaultCaptureOptions =
            "rtsp_transport;tcp|stimeout;5000000|fflags;nobuffer|flags;low_delay|max_delay;500000|reorder_queue_size;0";

        private static int _configured;

        /// <summary>
        /// FFMPEG capture sozlamalarini o'rnatadi. Takroriy chaqiruvlar e'tiborsiz qoldiriladi
        /// (thread-safe, Interlocked orqali) — sozlama process-global bo'lgani uchun uni
        /// keyinroq o'zgartirish ochiq oqimlar bilan poyga hosil qiladi.
        /// </summary>
        /// <param name="config">
        /// Ixtiyoriy. Berilsa <c>Camera:FfmpegOptions</c> kaliti default o'rniga ishlatiladi
        /// (turli kameralar/NVR'lar turlicha flaglarni talab qilishi mumkin).
        /// </param>
        public static void Configure(IConfiguration? config = null)
        {
            if (Interlocked.Exchange(ref _configured, 1) != 0) return;

            // Agar operator muhit o'zgaruvchisini tashqaridan bergan bo'lsa — uni buzmaymiz.
            var existing = Environment.GetEnvironmentVariable(CaptureOptionsVariable);
            if (!string.IsNullOrWhiteSpace(existing)) return;

            var options = config?[ConfigKey];
            if (string.IsNullOrWhiteSpace(options))
                options = DefaultCaptureOptions;

            Environment.SetEnvironmentVariable(CaptureOptionsVariable, options);
        }
    }
}
