using Microsoft.AspNetCore.Http;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class PhotoStorageService : IPhotoStorageService
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        // data: URL uchun ruxsat etilgan MIME turlari
        private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

        private readonly IWebHostEnvironment _env;

        public PhotoStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string?> SavePhotoAsync(IFormFile? file, string? base64DataUrl, string subfolder)
        {
            var folderAbs = Path.Combine(_env.WebRootPath, "uploads", subfolder);
            Directory.CreateDirectory(folderAbs);

            if (file != null && file.Length > 0)
            {
                if (file.Length > MaxBytes)
                    throw new InvalidOperationException("Rasm hajmi 5 MB dan oshmasligi kerak.");

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    throw new InvalidOperationException("Faqat .jpg, .jpeg, .png, .webp formatlari qabul qilinadi.");

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var pathAbs = Path.Combine(folderAbs, fileName);

                await using var stream = File.Create(pathAbs);
                await file.CopyToAsync(stream);

                return $"/uploads/{subfolder}/{fileName}";
            }

            if (!string.IsNullOrWhiteSpace(base64DataUrl))
            {
                var (bytes, ext) = ParseDataUrl(base64DataUrl);

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var pathAbs = Path.Combine(folderAbs, fileName);
                await File.WriteAllBytesAsync(pathAbs, bytes);

                return $"/uploads/{subfolder}/{fileName}";
            }

            return null;
        }

        public void DeletePhoto(string? webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath)) return;
            if (!webPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;

            // Path traversal himoyasi: normallashtirilgan yo'l "wwwroot/uploads" ichida
            // qolishini majburiy tekshiramiz ("/uploads/../../appsettings.json" kabi
            // qiymatlar wwwroot'dan tashqariga chiqib ketmasligi uchun).
            string uploadsRoot, abs;
            try
            {
                uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
                var relative = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                abs = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));
            }
            catch
            {
                return; // noto'g'ri yo'l — hech narsa o'chirmaymiz
            }

            var prefix = uploadsRoot.EndsWith(Path.DirectorySeparatorChar)
                ? uploadsRoot
                : uploadsRoot + Path.DirectorySeparatorChar;

            if (!abs.StartsWith(prefix, StringComparison.Ordinal))
                return;

            if (File.Exists(abs))
            {
                try { File.Delete(abs); } catch { /* ignore */ }
            }
        }

        private static (byte[] Bytes, string Extension) ParseDataUrl(string dataUrl)
        {
            // Format: data:image/png;base64,iVBORw0KGgo...
            var commaIdx = dataUrl.IndexOf(',');
            if (commaIdx < 0)
                throw new InvalidOperationException("Rasm formati noto'g'ri.");

            var header = dataUrl.Substring(0, commaIdx);
            var payload = dataUrl.Substring(commaIdx + 1);

            // 1) Header "data:image/..." bo'lishi SHART
            if (!header.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Faqat rasm (data:image/...) qabul qilinadi.");

            // 2) MIME turi oq ro'yxatda bo'lishi kerak
            var mime = header.Substring("data:".Length);
            var semicolon = mime.IndexOf(';');
            if (semicolon >= 0) mime = mime.Substring(0, semicolon);

            if (!AllowedMimeTypes.TryGetValue(mime.Trim(), out var ext))
                throw new InvalidOperationException("Faqat image/jpeg, image/png, image/webp formatlari qabul qilinadi.");

            // 3) Hajmni DEKODDAN OLDIN taxminlab rad etamiz —
            //    aks holda 100 MB'lik base64 avval xotiraga dekodlanardi.
            var approximateBytes = (long)payload.Length / 4 * 3;
            if (approximateBytes > MaxBytes)
                throw new InvalidOperationException("Rasm hajmi 5 MB dan oshmasligi kerak.");

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch
            {
                throw new InvalidOperationException("Rasm base64 ma'lumotlari noto'g'ri.");
            }

            // 4) Aniq hajmni ham tekshiramiz
            if (bytes.LongLength > MaxBytes)
                throw new InvalidOperationException("Rasm hajmi 5 MB dan oshmasligi kerak.");

            return (bytes, ext);
        }
    }
}
