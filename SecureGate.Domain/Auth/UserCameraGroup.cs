using System.ComponentModel.DataAnnotations.Schema;
using SecureGate.Domain;

namespace SecureGate.Domain.Auth
{
    // Admin foydalanuvchini ma'lum kamera guruh(lar)iga biriktiruvchi join entity.
    // Agar admin uchun bitta ham yozuv bo'lmasa — u barcha kameralarni ko'radi (default).
    // Agar yozuv(lar) bor bo'lsa — faqat tanlangan guruhlar va ulardagi kameralarni ko'radi.
    // SuperAdmin har doim hammasini ko'radi (bu cheklov undan o'tib ketmaydi).
    public class UserCameraGroup
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public AppUser? User { get; set; }

        public int CameraGroupId { get; set; }
        [ForeignKey(nameof(CameraGroupId))]
        public CameraGroup? CameraGroup { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
