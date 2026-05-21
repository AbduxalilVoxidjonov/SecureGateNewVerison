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

        private readonly IDataProtector _protector;

        public CameraCredentialProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
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
            catch (CryptographicException)
            {
                // Eski plain-text yozuv (multitenancy migratsiyadan oldin) —
                // shifrlanmagan holda qaytaramiz. Foydalanuvchi keyinroq
                // tahrirlaganida yangi qiymat shifrlanadi.
                return cipherText;
            }
        }
    }
}
