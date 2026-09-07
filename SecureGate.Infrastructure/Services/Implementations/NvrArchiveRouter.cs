using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations;

/// <summary>
/// Vendorga qarab mos <see cref="INvrArchiveService"/> ni tanlaydigan "yo'naltiruvchi".
///
/// <para>
/// O'zi ham <see cref="INvrArchiveService"/> ni implement qiladi — shu sababli
/// controller'lar oddiygina <c>INvrArchiveService</c> ni inject qiladi va qaysi vendor
/// ekanini bilishi shart emas.
/// </para>
///
/// <para><b>Yangi vendor qo'shish:</b> yangi <c>XyzNvrArchiveService</c> yoziladi va
/// Program.cs dagi ro'yxatga bitta qator qo'shiladi — bu klass o'zgarmaydi.</para>
///
/// <para>
/// Konstruktorga vendorlar <b>ochiq ro'yxat</b> bilan beriladi (DI enumerable orqali emas) —
/// aks holda router o'zi ham <c>INvrArchiveService</c> bo'lgani uchun o'zini o'ziga
/// qo'shib, cheksiz rekursiya hosil bo'lardi.
/// </para>
///
/// <para>Holatsiz — <b>Singleton</b>.</para>
/// </summary>
public sealed class NvrArchiveRouter : INvrArchiveService, INvrArchiveResolver
{
    private readonly IReadOnlyList<INvrArchiveService> _services;
    private readonly ILogger<NvrArchiveRouter> _logger;

    public NvrArchiveRouter(IEnumerable<INvrArchiveService> services, ILogger<NvrArchiveRouter> logger)
    {
        _services = services.Where(s => s is not NvrArchiveRouter).ToArray();
        _logger = logger;
    }

    // ===== INvrArchiveResolver =====

    public INvrArchiveService? Resolve(Camera camera)
    {
        if (camera is null) return null;

        foreach (var service in _services)
        {
            try
            {
                if (service.Supports(camera)) return service;
            }
            catch (Exception ex)
            {
                // Bitta vendor implementatsiyasi xato bersa — qolganlarini sinashda davom etamiz.
                _logger.LogWarning(ex, "{Service}.Supports xato berdi (kamera #{Id})",
                    service.GetType().Name, camera.Id);
            }
        }

        return null;
    }

    public bool IsSupported(Camera camera) => Resolve(camera) is not null;

    public INvrArchiveService Require(Camera camera) =>
        Resolve(camera) ?? throw new InvalidOperationException(
            $"'{camera?.Name}' kamerasi uchun arxiv qo'llab-quvvatlanmaydi. " +
            "Arxiv NVR kanali sifatida sozlangan qurilmalar uchun ishlaydi " +
            "(model, qurilma turi 'NVR kanali', kanal raqami va IP manzil to'ldirilgan bo'lishi kerak).");

    // ===== INvrArchiveService (delegatsiya) =====

    public bool Supports(Camera camera) => IsSupported(camera);

    public Task<IReadOnlyList<ArchiveSegment>> SearchAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        Require(camera).SearchAsync(camera, fromUtc, toUtc, ct);

    public Task<Stream> OpenPlaybackAsync(
        Camera camera, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        Require(camera).OpenPlaybackAsync(camera, fromUtc, toUtc, ct);
}
