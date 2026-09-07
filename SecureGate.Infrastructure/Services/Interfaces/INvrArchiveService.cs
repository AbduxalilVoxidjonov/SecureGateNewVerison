namespace SecureGate.Infrastructure.Services.Interfaces;

/// <summary>NVR arxividagi bitta yozuv bo'lagi (segment).</summary>
/// <param name="StartUtc">Bo'lak boshlanishi (UTC).</param>
/// <param name="EndUtc">Bo'lak tugashi (UTC).</param>
/// <param name="SizeBytes">Hajmi (baytda), qurilma bergan bo'lsa. Aks holda null.</param>
/// <param name="TrackId">Qurilmadagi track/kanal identifikatori (Hikvision: "101", "301"...).</param>
public sealed record ArchiveSegment(DateTime StartUtc, DateTime EndUtc, long? SizeBytes, string? TrackId);

/// <summary>
/// NVR (yoki kameraning o'z SD kartasi) arxividan eski yozuvlarni o'qish xizmati.
/// Har bir vendor uchun alohida implementatsiya bo'ladi (Hikvision, Dahua, ...);
/// mos keluvchisi <see cref="Supports"/> orqali tanlanadi — qarang <see cref="INvrArchiveResolver"/>.
/// </summary>
public interface INvrArchiveService
{
    /// <summary>Bu kamera/kanal uchun arxiv qo'llab-quvvatlanadimi.</summary>
    bool Supports(Camera camera);

    /// <summary>
    /// Berilgan vaqt oralig'idagi yozuv bo'laklarini qidiradi.
    /// Qurilma javob bermasa yoki xato qaytarsa — <see cref="InvalidOperationException"/>.
    /// </summary>
    Task<IReadOnlyList<ArchiveSegment>> SearchAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Berilgan oraliq uchun mp4 oqimi. Chaqiruvchi Stream'ni dispose qiladi.</summary>
    Task<Stream> OpenPlaybackAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

/// <summary>
/// Kamera vendoriga qarab mos <see cref="INvrArchiveService"/> ni tanlaydi.
/// DI'da barcha implementatsiyalar <c>INvrArchiveService</c> sifatida ro'yxatga olinadi,
/// resolver esa ularni <c>IEnumerable&lt;INvrArchiveService&gt;</c> orqali oladi.
/// Yangi vendor qo'shish = yangi implementatsiya + bitta <c>AddSingleton</c> qatori.
/// </summary>
public interface INvrArchiveResolver
{
    /// <summary>Mos servis. Topilmasa null.</summary>
    INvrArchiveService? Resolve(Camera camera);

    /// <summary>Bu kamera uchun arxiv umuman mavjudmi.</summary>
    bool IsSupported(Camera camera);

    /// <summary>Mos servisni qaytaradi, topilmasa tushunarli xabar bilan xato tashlaydi.</summary>
    INvrArchiveService Require(Camera camera);
}
