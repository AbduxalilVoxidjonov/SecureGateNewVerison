namespace SecureGate.Infrastructure.Services.Interfaces
{
    // FaceAiSharp ustidagi pastki sath. Bu yerda hech qanday DB yoki HTTP yo'q —
    // faqat: bayt/file → yuzlarni aniqlash → ArcFace 512-d embedding.
    public interface IFaceRecognitionEngine
    {
        // ArcFace embeddingining o'lchami (FaceAiSharp Bundle modeli uchun 512).
        int EmbeddingSize { get; }

        // Bitta rasmda **eng katta** yuzni topib, embedding qaytaradi.
        // Profil rasmlari (xodim, o'quvchi anketasi) uchun mo'ljallangan.
        Task<float[]?> ComputeEmbeddingFromFileAsync(string absolutePath, CancellationToken ct = default);

        // RTSP frame'idan barcha yuzlarni topib, har biri uchun embedding qaytaradi.
        // Yuz topilmasa — bo'sh ro'yxat.
        Task<IReadOnlyList<DetectedFace>> DetectFacesAsync(byte[] imageBytes, CancellationToken ct = default);

        // Ikkita L2-normalizatsiya qilingan embeddinglarning kosinus o'xshashligi.
        // FaceAiSharp ArcFace chiqishi allaqachon L2-normalizatsiyalangan, shuning uchun
        // bu oddiy dot product. Diapazon: [-1.0 .. 1.0], yaqinroq → o'xshashroq.
        float Similarity(float[] a, float[] b);
    }

    public sealed record DetectedFace(float[] Embedding, float DetectionConfidence, BoundingBox Box);

    public readonly record struct BoundingBox(int X, int Y, int Width, int Height);
}
