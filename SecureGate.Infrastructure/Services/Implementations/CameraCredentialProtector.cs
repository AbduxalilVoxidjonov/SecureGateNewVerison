using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class CameraCredentialProtector : ICameraCredentialProtector
    {
        // "Purpose string" — kalit hosil qilishda ishlatiladigan kontekst.
        // Versiyalashtirilgan, kelajakda algoritm yangilash kerak bo'lsa
        // v2 qilib yangi protector qo'shish mumkin.
        private const string Purpose = "Camera.Credentials.v1";

        // ASP.NET Core DataProtection payload'lari base64url'da bo'ladi va
        // magic header bilan boshlanadi ("CfDJ8..." — 0x09F0C9F0 marker).
        private const string ProtectedPayloadPrefix = "CfDJ8";

        private readonly IDataProtector _protector;
        private readonly ILogger<CameraCredentialProtector> _logger;

        public CameraCredentialProtector(
            IDataProtectionProvider provider,
            ILogger<CameraCredentialProtector> logger)
        {
            _protector = provider.CreateProtector(Purpose);
            _logger = logger;
        }

        public string? Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            return _protector.Protect(plainText);
        }

        public string? Unprotect(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            try
            {
                return _protector.Unprotect(cipherText);
            }
            catch (CryptographicException ex)
            {
                if (LooksLikeProtectedPayload(cipherText))
                {
                    // Qiymat SHIFRLANGAN ko'rinishda, lekin ochilmadi —
                    // odatda DataProtection kalitlari yo'qolgan/almashgan
                    // (Program.cs da PersistKeysToFileSystem sozlanmagan bo'lsa shunday bo'ladi).
                    // Shifrlangan matnni parol sifatida QAYTARMAYMIZ — bu qurilmaga
                    // axlat parol yuborilishiga va loglarda sirning oshkor bo'lishiga olib keladi.
                    _logger.LogWarning(ex,
                        "Kamera hisob ma'lumotini deshifrlab bo'lmadi (DataProtection kaliti yo'qolgan bo'lishi mumkin). " +
                        "Parolni qayta kiritish kerak.");
                    return null;
                }

                // Qiymat shifrlangan formatga umuman o'xshamaydi — migratsiyadan oldingi
                // eski plain-text yozuv. Uni o'zini qaytaramiz (tahrirlanganda shifrlanadi).
                _logger.LogWarning(
                    "Kamera hisob ma'lumoti shifrlanmagan (eski yozuv) — plain-text sifatida ishlatildi. " +
                    "Kamerani tahrirlab saqlang, shunda qiymat shifrlanadi.");
                return cipherText;
            }
        }

        // DataProtection payload'i: base64url alifbosi, magic prefiks, uzun (>= 32 belgi).
        private static bool LooksLikeProtectedPayload(string value)
        {
            if (value.StartsWith(ProtectedPayloadPrefix, StringComparison.Ordinal)) return true;
            if (value.Length < 32) return false;

            foreach (var ch in value)
            {
                var isBase64Url =
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch == '-' || ch == '_' || ch == '=';
                if (!isBase64Url) return false;
            }

            return true;
        }
    }
}
