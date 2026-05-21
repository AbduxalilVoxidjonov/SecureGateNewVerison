namespace SecureGate.Infrastructure.Services.Interfaces
{
    // DB'dagi barcha faol foydalanuvchi/xodim encoding'larini xotirada saqlab turadi.
    // Har bir RTSP frame uchun DB'ga bormaslik uchun. Davriy ravishda yangilanadi.
    public interface IKnownFaceCache
    {
        IReadOnlyList<KnownFace> Snapshot { get; }

        // Tashqaridan DB'dan qayta yuklash uchun (masalan, foydalanuvchi qo'shilganidan keyin).
        Task ReloadAsync(CancellationToken ct = default);

        DateTime LastReloadAt { get; }
    }

    public sealed record KnownFace(
        string PersonType,  // "Student" | "Teacher" | "Staff"
        int PersonId,
        string FullName,
        float[] Embedding);
}
