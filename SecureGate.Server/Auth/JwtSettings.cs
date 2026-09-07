using System.Text;

namespace SecureGate.Api.Auth
{
    public class JwtSettings
    {
        /// <summary>HMAC-SHA256 uchun kalitning minimal uzunligi (bayt).</summary>
        public const int MinimumKeyBytes = 32;

        public string Issuer { get; set; } = "SecureGate.Api";
        public string Audience { get; set; } = "SecureGate.Clients";
        public string Key { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;

        /// <summary>
        /// Startup'da chaqiriladi. JWT imzo kaliti yo'q yoki juda qisqa bo'lsa
        /// ilova umuman ishga tushmasligi kerak (imzoni brute-force qilish mumkin bo'lib qoladi).
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                throw new InvalidOperationException(
                    "JWT imzo kaliti (Jwt:Key) sozlanmagan. Uni environment variable yoki user-secrets orqali bering " +
                    "(masalan: Jwt__Key). appsettings.json ga yozmang.");
            }

            var byteCount = Encoding.UTF8.GetByteCount(Key);
            if (byteCount < MinimumKeyBytes)
            {
                throw new InvalidOperationException(
                    $"JWT imzo kaliti (Jwt:Key) juda qisqa: {byteCount} bayt. " +
                    $"HMAC-SHA256 uchun kamida {MinimumKeyBytes} bayt (256 bit) talab qilinadi.");
            }

            if (string.IsNullOrWhiteSpace(Issuer))
                throw new InvalidOperationException("Jwt:Issuer sozlanmagan.");

            if (string.IsNullOrWhiteSpace(Audience))
                throw new InvalidOperationException("Jwt:Audience sozlanmagan.");

            if (AccessTokenMinutes <= 0)
                throw new InvalidOperationException("Jwt:AccessTokenMinutes musbat son bo'lishi kerak.");

            if (RefreshTokenDays <= 0)
                throw new InvalidOperationException("Jwt:RefreshTokenDays musbat son bo'lishi kerak.");
        }
    }
}
