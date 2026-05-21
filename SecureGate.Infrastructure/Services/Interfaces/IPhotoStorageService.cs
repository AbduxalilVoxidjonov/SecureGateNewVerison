using Microsoft.AspNetCore.Http;

namespace SecureGate.Infrastructure.Services.Interfaces
{
    public interface IPhotoStorageService
    {
        /// <summary>
        /// IFormFile yoki base64 data URL'dan birini saqlaydi.
        /// Web yo'lini qaytaradi (masalan: /uploads/users/abc.jpg) yoki null.
        /// </summary>
        Task<string?> SavePhotoAsync(IFormFile? file, string? base64DataUrl, string subfolder);

        void DeletePhoto(string? webPath);
    }
}
