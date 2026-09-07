namespace SecureGate.Infrastructure.Services.Interfaces
{
    // DB'dagi barcha faol foydalanuvchi/xodim encoding'larini xotirada saqlab turadi.
    // Har bir RTSP frame uchun DB'ga bormaslik uchun. Davriy ravishda yangilanadi.
    public interface IKnownFaceCache
    {
        IReadOnlyList<KnownFace> Snapshot { get; }

        /// <summary>
        /// Cache'ni DB'dan qayta yuklaydi (masalan, foydalanuvchi qo'shilgan/bloklanganidan keyin).
        /// </summary>
        /// <param name="force">
        /// <c>false</c> (default) — boshqa reload allaqachon ketayotgan bo'lsa chaqiruv
        /// darhol qaytadi (fire-and-forget, davriy timer uchun).
        /// <c>true</c> — ketayotgan reload tugashini KUTADI va o'zi ham to'liq yuklaydi.
        /// Worker start'ida cache bo'sh qolmasligi uchun <c>true</c> ishlatiladi.
        /// </param>
        Task ReloadAsync(CancellationToken ct = default, bool force = false);

        DateTime LastReloadAt { get; }
    }

    public sealed record KnownFace(
        string PersonType,  // "Student" | "Teacher" | "Staff"
        int PersonId,
        string FullName,
        float[] Embedding);
}
