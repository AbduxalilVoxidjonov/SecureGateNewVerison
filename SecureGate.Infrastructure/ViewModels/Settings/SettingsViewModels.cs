using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Settings
{
    public class NotificationsSettingsViewModel
    {
        [Display(Name = "Brauzer ichidagi bildirishnomalar")]
        public bool InAppEnabled { get; set; } = true;

        [Display(Name = "Email orqali bildirishnomalar")]
        public bool EmailEnabled { get; set; }

        [Display(Name = "SMS orqali bildirishnomalar")]
        public bool SmsEnabled { get; set; }

        [Display(Name = "Telegram orqali bildirishnomalar")]
        public bool TelegramEnabled { get; set; }

        [Display(Name = "Tovushli ogohlantirish")]
        public bool SoundEnabled { get; set; } = true;

        // Hodisalar
        [Display(Name = "Ruxsatsiz kirish urinishi")]
        public bool NotifyOnDenied { get; set; } = true;

        [Display(Name = "Bloklangan foydalanuvchi urinishi")]
        public bool NotifyOnBlocked { get; set; } = true;

        [Display(Name = "Kamera oflayn bo'lganda")]
        public bool NotifyOnCameraOffline { get; set; } = true;

        [Display(Name = "Turniket nosozliklari")]
        public bool NotifyOnTurnstileError { get; set; } = true;

        [Display(Name = "Yangi foydalanuvchi yaratilganda")]
        public bool NotifyOnUserCreated { get; set; }

        [Display(Name = "Qabul qiluvchi email manzili")]
        [EmailAddress(ErrorMessage = "Email noto'g'ri formatda")]
        public string? RecipientEmail { get; set; }

        [Display(Name = "Qabul qiluvchi telefon raqami")]
        public string? RecipientPhone { get; set; }
    }

    public class IntegrationsSettingsViewModel
    {
        // SMTP
        [Display(Name = "SMTP server")]
        public string? SmtpHost { get; set; }

        [Display(Name = "SMTP port")]
        public int? SmtpPort { get; set; } = 587;

        [Display(Name = "SMTP foydalanuvchi")]
        public string? SmtpUsername { get; set; }

        [Display(Name = "SMTP parol")]
        [DataType(DataType.Password)]
        public string? SmtpPassword { get; set; }

        [Display(Name = "SMTP SSL/TLS")]
        public bool SmtpUseSsl { get; set; } = true;

        [Display(Name = "Yuboruvchi email")]
        public string? SmtpFromEmail { get; set; }

        // Telegram
        [Display(Name = "Telegram bot tokeni")]
        public string? TelegramBotToken { get; set; }

        [Display(Name = "Telegram chat ID")]
        public string? TelegramChatId { get; set; }

        // SMS gateway (Eskiz.uz, PlayMobile va h.k.)
        [Display(Name = "SMS provayder")]
        public string? SmsProvider { get; set; } = "Eskiz.uz";

        [Display(Name = "SMS API URL")]
        public string? SmsApiUrl { get; set; }

        [Display(Name = "SMS API kalit/token")]
        [DataType(DataType.Password)]
        public string? SmsApiKey { get; set; }

        [Display(Name = "SMS yuboruvchi nomi")]
        public string? SmsSender { get; set; }
    }

    public class ApiSettingsViewModel
    {
        [Display(Name = "API kaliti")]
        public string ApiKey { get; set; } = string.Empty;

        [Display(Name = "API yaratilgan sana")]
        public string? ApiKeyCreatedAt { get; set; }

        [Display(Name = "Webhook URL")]
        [Url(ErrorMessage = "URL noto'g'ri formatda")]
        public string? WebhookUrl { get; set; }

        [Display(Name = "Webhook maxfiy kalit (HMAC)")]
        public string? WebhookSecret { get; set; }

        [Display(Name = "Webhookni yoqish")]
        public bool WebhookEnabled { get; set; }

        // Hodisalar
        [Display(Name = "access.granted")]
        public bool SubscribeAccessGranted { get; set; } = true;

        [Display(Name = "access.denied")]
        public bool SubscribeAccessDenied { get; set; } = true;

        [Display(Name = "camera.offline")]
        public bool SubscribeCameraOffline { get; set; }

        [Display(Name = "turnstile.error")]
        public bool SubscribeTurnstileError { get; set; }

        [Display(Name = "user.blocked")]
        public bool SubscribeUserBlocked { get; set; }
    }
}
