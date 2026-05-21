namespace SecureGate.Infrastructure.Services.Interfaces
{
    // Kamera credentials'ini (asosan parolni) DB'ga yozishdan oldin shifrlash
    // va o'qish paytida ochish uchun servis. ASP.NET Core DataProtection API
    // asosida ishlaydi — har bir deployment o'z kalitiga ega.
    public interface ICameraCredentialProtector
    {
        // Plain-text -> shifrlangan matn. null/empty bo'lsa o'zgartirmaydi.
        string? Protect(string? plainText);

        // Shifrlangan matn -> plain-text. Eski (shifrlanmagan) qiymat bo'lsa
        // uni o'zgartirmasdan qaytaradi (orqaga moslik).
        string? Unprotect(string? cipherText);
    }
}
