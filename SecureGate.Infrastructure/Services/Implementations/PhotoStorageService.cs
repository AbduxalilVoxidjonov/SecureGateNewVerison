using Microsoft.AspNetCore.Http;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    public class PhotoStorageService : IPhotoStorageService
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
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
                if (bytes.Length > MaxBytes)
                    throw new InvalidOperationException("Rasm hajmi 5 MB dan oshmasligi kerak.");

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

            var relative = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var abs = Path.Combine(_env.WebRootPath, relative);
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

            string ext = ".png";
            if (header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase)) ext = ".jpg";
            else if (header.Contains("image/webp", StringComparison.OrdinalIgnoreCase)) ext = ".webp";

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch
            {
                throw new InvalidOperationException("Rasm base64 ma'lumotlari noto'g'ri.");
            }

            return (bytes, ext);
        }
    }
}
