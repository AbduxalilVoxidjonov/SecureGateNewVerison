using Microsoft.AspNetCore.Mvc;
using SecureGate.Api.Filters;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Services.Interfaces;
using SecureGate.Infrastructure.ViewModels.Settings;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Cryptography;

namespace SecureGate.Api.Controllers
{
    [Route("api/settings")]
    [HasPermission(Permission.SettingsManage)]
    public class SettingsController : ApiControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        // ===================== a) BILDIRISHNOMALAR =====================

        [HttpGet("notifications")]
        [SwaggerOperation(Summary = "Bildirishnoma sozlamalarini olish")]
        public async Task<IActionResult> GetNotifications()
        {
            var keys = new[]
            {
                Keys.InAppEnabled, Keys.EmailEnabled, Keys.SmsEnabled, Keys.TelegramEnabled, Keys.SoundEnabled,
                Keys.NotifyOnDenied, Keys.NotifyOnBlocked, Keys.NotifyOnCameraOffline,
                Keys.NotifyOnTurnstileError, Keys.NotifyOnUserCreated,
                Keys.RecipientEmail, Keys.RecipientPhone
            };
            var values = await _settingService.GetManyAsync(keys);

            var model = new NotificationsSettingsViewModel
            {
                InAppEnabled = Bool(values, Keys.InAppEnabled, true),
                EmailEnabled = Bool(values, Keys.EmailEnabled),
                SmsEnabled = Bool(values, Keys.SmsEnabled),
                TelegramEnabled = Bool(values, Keys.TelegramEnabled),
                SoundEnabled = Bool(values, Keys.SoundEnabled, true),
                NotifyOnDenied = Bool(values, Keys.NotifyOnDenied, true),
                NotifyOnBlocked = Bool(values, Keys.NotifyOnBlocked, true),
                NotifyOnCameraOffline = Bool(values, Keys.NotifyOnCameraOffline, true),
                NotifyOnTurnstileError = Bool(values, Keys.NotifyOnTurnstileError, true),
                NotifyOnUserCreated = Bool(values, Keys.NotifyOnUserCreated),
                RecipientEmail = values[Keys.RecipientEmail],
                RecipientPhone = values[Keys.RecipientPhone]
            };
            return OkResponse(model);
        }

        [HttpPut("notifications")]
        [SwaggerOperation(Summary = "Bildirishnoma sozlamalarini saqlash")]
        public async Task<IActionResult> UpdateNotifications([FromBody] NotificationsSettingsViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            await _settingService.SetManyAsync(new Dictionary<string, string?>
            {
                [Keys.InAppEnabled] = B(model.InAppEnabled),
                [Keys.EmailEnabled] = B(model.EmailEnabled),
                [Keys.SmsEnabled] = B(model.SmsEnabled),
                [Keys.TelegramEnabled] = B(model.TelegramEnabled),
                [Keys.SoundEnabled] = B(model.SoundEnabled),
                [Keys.NotifyOnDenied] = B(model.NotifyOnDenied),
                [Keys.NotifyOnBlocked] = B(model.NotifyOnBlocked),
                [Keys.NotifyOnCameraOffline] = B(model.NotifyOnCameraOffline),
                [Keys.NotifyOnTurnstileError] = B(model.NotifyOnTurnstileError),
                [Keys.NotifyOnUserCreated] = B(model.NotifyOnUserCreated),
                [Keys.RecipientEmail] = model.RecipientEmail,
                [Keys.RecipientPhone] = model.RecipientPhone
            });

            return OkResponse("Saqlandi.");
        }

        // ===================== b) INTEGRATSIYALAR =====================

        [HttpGet("integrations")]
        [SwaggerOperation(Summary = "Integratsiya sozlamalari (SMTP, Telegram, SMS)")]
        public async Task<IActionResult> GetIntegrations()
        {
            var keys = new[]
            {
                Keys.SmtpHost, Keys.SmtpPort, Keys.SmtpUsername, Keys.SmtpPassword,
                Keys.SmtpUseSsl, Keys.SmtpFromEmail,
                Keys.TelegramBotToken, Keys.TelegramChatId,
                Keys.SmsProvider, Keys.SmsApiUrl, Keys.SmsApiKey, Keys.SmsSender
            };
            var values = await _settingService.GetManyAsync(keys);

            var model = new IntegrationsSettingsViewModel
            {
                SmtpHost = values[Keys.SmtpHost],
                SmtpPort = int.TryParse(values[Keys.SmtpPort], out var port) ? port : 587,
                SmtpUsername = values[Keys.SmtpUsername],
                SmtpPassword = values[Keys.SmtpPassword],
                SmtpUseSsl = Bool(values, Keys.SmtpUseSsl, true),
                SmtpFromEmail = values[Keys.SmtpFromEmail],
                TelegramBotToken = values[Keys.TelegramBotToken],
                TelegramChatId = values[Keys.TelegramChatId],
                SmsProvider = values[Keys.SmsProvider] ?? "Eskiz.uz",
                SmsApiUrl = values[Keys.SmsApiUrl],
                SmsApiKey = values[Keys.SmsApiKey],
                SmsSender = values[Keys.SmsSender]
            };
            return OkResponse(model);
        }

        [HttpPut("integrations")]
        [SwaggerOperation(Summary = "Integratsiya sozlamalarini saqlash")]
        public async Task<IActionResult> UpdateIntegrations([FromBody] IntegrationsSettingsViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            await _settingService.SetManyAsync(new Dictionary<string, string?>
            {
                [Keys.SmtpHost] = model.SmtpHost,
                [Keys.SmtpPort] = model.SmtpPort?.ToString(),
                [Keys.SmtpUsername] = model.SmtpUsername,
                [Keys.SmtpPassword] = model.SmtpPassword,
                [Keys.SmtpUseSsl] = B(model.SmtpUseSsl),
                [Keys.SmtpFromEmail] = model.SmtpFromEmail,
                [Keys.TelegramBotToken] = model.TelegramBotToken,
                [Keys.TelegramChatId] = model.TelegramChatId,
                [Keys.SmsProvider] = model.SmsProvider,
                [Keys.SmsApiUrl] = model.SmsApiUrl,
                [Keys.SmsApiKey] = model.SmsApiKey,
                [Keys.SmsSender] = model.SmsSender
            });

            return OkResponse("Saqlandi.");
        }

        // ===================== c) API / WEBHOOKS =====================

        [HttpGet("api")]
        [SwaggerOperation(Summary = "API kalit va Webhook sozlamalarini olish")]
        public async Task<IActionResult> GetApi()
        {
            var keys = new[]
            {
                Keys.ApiKey, Keys.ApiKeyCreatedAt, Keys.WebhookUrl, Keys.WebhookSecret, Keys.WebhookEnabled,
                Keys.SubAccessGranted, Keys.SubAccessDenied, Keys.SubCameraOffline,
                Keys.SubTurnstileError, Keys.SubUserBlocked
            };
            var values = await _settingService.GetManyAsync(keys);

            var apiKey = values[Keys.ApiKey];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = GenerateApiKey();
                await _settingService.SetManyAsync(new Dictionary<string, string?>
                {
                    [Keys.ApiKey] = apiKey,
                    [Keys.ApiKeyCreatedAt] = DateTime.UtcNow.ToString("O")
                });
                values[Keys.ApiKey] = apiKey;
                values[Keys.ApiKeyCreatedAt] = DateTime.UtcNow.ToString("O");
            }

            var model = new ApiSettingsViewModel
            {
                ApiKey = apiKey,
                ApiKeyCreatedAt = values[Keys.ApiKeyCreatedAt],
                WebhookUrl = values[Keys.WebhookUrl],
                WebhookSecret = values[Keys.WebhookSecret],
                WebhookEnabled = Bool(values, Keys.WebhookEnabled),
                SubscribeAccessGranted = Bool(values, Keys.SubAccessGranted, true),
                SubscribeAccessDenied = Bool(values, Keys.SubAccessDenied, true),
                SubscribeCameraOffline = Bool(values, Keys.SubCameraOffline),
                SubscribeTurnstileError = Bool(values, Keys.SubTurnstileError),
                SubscribeUserBlocked = Bool(values, Keys.SubUserBlocked)
            };
            return OkResponse(model);
        }

        [HttpPut("api/webhook")]
        [SwaggerOperation(Summary = "Webhook sozlamalarini saqlash")]
        public async Task<IActionResult> UpdateWebhook([FromBody] ApiSettingsViewModel model)
        {
            if (!ModelState.IsValid) return ValidationFail();

            await _settingService.SetManyAsync(new Dictionary<string, string?>
            {
                [Keys.WebhookUrl] = model.WebhookUrl,
                [Keys.WebhookSecret] = model.WebhookSecret,
                [Keys.WebhookEnabled] = B(model.WebhookEnabled),
                [Keys.SubAccessGranted] = B(model.SubscribeAccessGranted),
                [Keys.SubAccessDenied] = B(model.SubscribeAccessDenied),
                [Keys.SubCameraOffline] = B(model.SubscribeCameraOffline),
                [Keys.SubTurnstileError] = B(model.SubscribeTurnstileError),
                [Keys.SubUserBlocked] = B(model.SubscribeUserBlocked)
            });

            return OkResponse("Saqlandi.");
        }

        [HttpPost("api/regenerate-key")]
        [SwaggerOperation(Summary = "API kalitni qaytadan yaratish")]
        public async Task<IActionResult> RegenerateKey()
        {
            var newKey = GenerateApiKey();
            await _settingService.SetManyAsync(new Dictionary<string, string?>
            {
                [Keys.ApiKey] = newKey,
                [Keys.ApiKeyCreatedAt] = DateTime.UtcNow.ToString("O")
            });
            return OkResponse(new { apiKey = newKey, createdAt = DateTime.UtcNow }, "Yangi API kaliti yaratildi.");
        }

        // ===================== d) RAW =====================

        [HttpGet]
        [SwaggerOperation(Summary = "Barcha sozlamalar (raw)")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _settingService.GetAllAsync();
            return OkResponse(list);
        }

        // ===================== Helpers =====================

        private static string GenerateApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return "sgk_" + Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "")
                .Substring(0, 40);
        }

        private static bool Bool(IDictionary<string, string?> dict, string key, bool fallback = false)
        {
            if (!dict.TryGetValue(key, out var v) || string.IsNullOrEmpty(v)) return fallback;
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string B(bool value) => value ? "true" : "false";

        private static class Keys
        {
            public const string InAppEnabled = "notify.inApp.enabled";
            public const string EmailEnabled = "notify.email.enabled";
            public const string SmsEnabled = "notify.sms.enabled";
            public const string TelegramEnabled = "notify.telegram.enabled";
            public const string SoundEnabled = "notify.sound.enabled";
            public const string NotifyOnDenied = "notify.on.accessDenied";
            public const string NotifyOnBlocked = "notify.on.blockedAttempt";
            public const string NotifyOnCameraOffline = "notify.on.cameraOffline";
            public const string NotifyOnTurnstileError = "notify.on.turnstileError";
            public const string NotifyOnUserCreated = "notify.on.userCreated";
            public const string RecipientEmail = "notify.recipient.email";
            public const string RecipientPhone = "notify.recipient.phone";

            public const string SmtpHost = "integration.smtp.host";
            public const string SmtpPort = "integration.smtp.port";
            public const string SmtpUsername = "integration.smtp.username";
            public const string SmtpPassword = "integration.smtp.password";
            public const string SmtpUseSsl = "integration.smtp.useSsl";
            public const string SmtpFromEmail = "integration.smtp.fromEmail";

            public const string TelegramBotToken = "integration.telegram.botToken";
            public const string TelegramChatId = "integration.telegram.chatId";

            public const string SmsProvider = "integration.sms.provider";
            public const string SmsApiUrl = "integration.sms.apiUrl";
            public const string SmsApiKey = "integration.sms.apiKey";
            public const string SmsSender = "integration.sms.sender";

            public const string ApiKey = "api.key";
            public const string ApiKeyCreatedAt = "api.key.createdAt";

            public const string WebhookUrl = "webhook.url";
            public const string WebhookSecret = "webhook.secret";
            public const string WebhookEnabled = "webhook.enabled";
            public const string SubAccessGranted = "webhook.sub.accessGranted";
            public const string SubAccessDenied = "webhook.sub.accessDenied";
            public const string SubCameraOffline = "webhook.sub.cameraOffline";
            public const string SubTurnstileError = "webhook.sub.turnstileError";
            public const string SubUserBlocked = "webhook.sub.userBlocked";
        }
    }
}
