namespace SecureGate.Infrastructure.Services.Interfaces
{
    // Python face-worker servis bilan aloqa qiluvchi HTTP klient.
    // C# tarafdan: foydalanuvchi rasmi uchun encoding hisoblanadi va FaceData'da saqlanadi.
    public interface IFaceRecognitionClient
    {
        // Web yo'li (masalan: "/uploads/users/abc.jpg") asosida absolyut yo'l topib,
        // Python servisga jo'natadi. Encoding (128-o'lchovli float vektor) qaytaradi
        // yoki Python yo'q/xato bo'lsa null.
        Task<float[]?> ComputeEmbeddingAsync(string webRelativePath, CancellationToken ct = default);

        // Python servisning sog'lig'ini tekshirish (admin sahifasida foydali).
        Task<bool> IsHealthyAsync(CancellationToken ct = default);
    }
}
