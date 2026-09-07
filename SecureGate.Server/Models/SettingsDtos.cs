namespace SecureGate.Api.Models
{
    /// <summary>
    /// Maxfiy sozlama qiymatlarini maskalash uchun yordamchi.
    /// </summary>
    public static class SecretMask
    {
        public const string Prefix = "••••••";

        /// <summary>
        /// Qiymat bo'lmasa null, bo'lsa "••••••" + oxirgi 4 belgi qaytaradi.
        /// </summary>
        public static string? Mask(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return value.Length <= 4 ? Prefix : Prefix + value[^4..];
        }

        /// <summary>
        /// Kelgan qiymat bo'sh yoki maskalangan ko'rinishda bo'lsa true —
        /// bunday holda eski qiymat o'zgartirilmasligi kerak.
        /// </summary>
        public static bool IsMaskedOrEmpty(string? incoming) =>
            string.IsNullOrWhiteSpace(incoming) || incoming.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>
        /// Sozlama kaliti maxfiymi? (*.password, *.secret, *.key, *.token, *.apiKey ...)
        /// </summary>
        public static bool IsSecretKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            var k = key.ToLowerInvariant();
            return k.EndsWith("password")
                || k.EndsWith("secret")
                || k.EndsWith("token")
                || k.EndsWith("key")
                || k.Contains("password.")
                || k.Contains("secret.")
                || k.Contains("token.")
                || k.Contains("apikey");
        }
    }

    /// <summary>GET /api/settings/integrations javobi — maxfiy qiymatlar maskalangan.</summary>
    public class IntegrationsSettingsResponseDto
    {
        public string? SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public bool HasSmtpPassword { get; set; }
        public bool SmtpUseSsl { get; set; }
        public string? SmtpFromEmail { get; set; }

        public string? TelegramBotToken { get; set; }
        public bool HasTelegramBotToken { get; set; }
        public string? TelegramChatId { get; set; }

        public string? SmsProvider { get; set; }
        public string? SmsApiUrl { get; set; }
        public string? SmsApiKey { get; set; }
        public bool HasSmsApiKey { get; set; }
        public string? SmsSender { get; set; }
    }

    /// <summary>GET /api/settings/api javobi — API kaliti va webhook secret maskalangan.</summary>
    public class ApiSettingsResponseDto
    {
        public string? ApiKey { get; set; }
        public bool HasApiKey { get; set; }
        public string? ApiKeyCreatedAt { get; set; }

        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }
        public bool HasWebhookSecret { get; set; }
        public bool WebhookEnabled { get; set; }

        public bool SubscribeAccessGranted { get; set; }
        public bool SubscribeAccessDenied { get; set; }
        public bool SubscribeCameraOffline { get; set; }
        public bool SubscribeTurnstileError { get; set; }
        public bool SubscribeUserBlocked { get; set; }
    }

    /// <summary>GET /api/settings (raw) javobi — maxfiy kalitlar maskalangan.</summary>
    public class SettingResponseDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
        public SettingType Type { get; set; }
        public bool IsSecret { get; set; }
        public bool HasValue { get; set; }
    }
}
