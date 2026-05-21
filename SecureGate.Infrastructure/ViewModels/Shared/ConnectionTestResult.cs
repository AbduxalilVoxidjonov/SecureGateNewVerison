namespace SecureGate.Infrastructure.ViewModels.Shared
{
    /// <summary>
    /// Qurilma (kamera/turniket) ulanishini "test ulanib ko'rish" natijasi.
    /// Hech narsa DB'ga yozilmaydi — faqat ulanish holati qaytariladi.
    /// </summary>
    public class ConnectionTestResult
    {
        /// <summary>Ulanish muvaffaqiyatli bo'ldimi.</summary>
        public bool Ok { get; set; }

        /// <summary>Foydalanuvchiga ko'rsatiladigan xabar (o'zbekcha).</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Test qancha vaqt olgani (ms).</summary>
        public long ElapsedMs { get; set; }

        /// <summary>Kamera bo'lsa — olingan kadr eni (px).</summary>
        public int? Width { get; set; }

        /// <summary>Kamera bo'lsa — olingan kadr balandligi (px).</summary>
        public int? Height { get; set; }

        public static ConnectionTestResult Success(string message, long elapsedMs, int? width = null, int? height = null)
            => new() { Ok = true, Message = message, ElapsedMs = elapsedMs, Width = width, Height = height };

        public static ConnectionTestResult Fail(string message, long elapsedMs = 0)
            => new() { Ok = false, Message = message, ElapsedMs = elapsedMs };
    }
}
