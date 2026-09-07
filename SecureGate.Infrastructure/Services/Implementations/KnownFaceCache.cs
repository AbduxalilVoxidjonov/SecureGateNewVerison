using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureGate.Data;
using SecureGate.Domain;
using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // Singleton — FaceData jadvalidagi barcha faol encoding'lar xotirada turadi.
    // CameraStreamWorker har frame uchun shu cache'dan o'qiydi, DB'ga bormaydi.
    //
    // Yangilanish:
    //   - Ishga tushganda bir marta
    //   - Har N soniya (config'da, default 60s) avtomatik
    //   - Tashqi chaqiruv orqali (UsersService/StaffService yangi rasm yuklaganida ham
    //     chaqirilishi mumkin; hozircha davriy yangilanish yetarli)
    public class KnownFaceCache : IKnownFaceCache, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IFaceRecognitionEngine _engine;
        private readonly ILogger<KnownFaceCache> _logger;
        private readonly Timer _timer;
        private readonly SemaphoreSlim _reloadGate = new(1, 1);

        private volatile IReadOnlyList<KnownFace> _cache = Array.Empty<KnownFace>();
        private volatile bool _disposed;

        public IReadOnlyList<KnownFace> Snapshot => _cache;
        public DateTime LastReloadAt { get; private set; }

        public KnownFaceCache(
            IServiceScopeFactory scopeFactory,
            IFaceRecognitionEngine engine,
            IConfiguration config,
            ILogger<KnownFaceCache> logger)
        {
            _scopeFactory = scopeFactory;
            _engine = engine;
            _logger = logger;

            var reloadSec = config.GetValue<int?>("FaceRecognition:KnownFaceReloadSeconds") ?? 60;
            var period = TimeSpan.FromSeconds(Math.Max(10, reloadSec));

            // Timer callback — async void semantikasi: hech qanday istisno tashqariga
            // chiqmasligi kerak, aks holda process yiqiladi.
            _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), period);
        }

        private async void OnTimerTick(object? state)
        {
            // To'liq himoyalangan: ReloadAsync ichida ham catch bor, lekin
            // async void'dan chiqadigan HAR QANDAY istisno process'ni o'ldiradi.
            try
            {
                if (_disposed) return;
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                try { _logger.LogError(ex, "KnownFaceCache timer callback'ida kutilmagan xato"); }
                catch { /* logger ham yiqilsa — jim qolamiz */ }
            }
        }

        public async Task ReloadAsync(CancellationToken ct = default, bool force = false)
        {
            if (_disposed) return;

            if (force)
            {
                // To'liq kutamiz — cache bo'sh qolmasligi kafolatlanadi.
                await _reloadGate.WaitAsync(ct);
            }
            else
            {
                // Boshqasi allaqachon yangilamoqda — o'tkazib yuboramiz.
                if (!await _reloadGate.WaitAsync(0, ct)) return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var rows = await db.FaceData
                    .Where(f => f.IsActive && f.FaceEncoding != null)
                    .Include(f => f.Student)
                    .Include(f => f.Teacher)
                    .Include(f => f.Staff)
                    .AsNoTracking()
                    .ToListAsync(ct);

                var list = new List<KnownFace>(rows.Count);
                int skipped = 0;
                foreach (var f in rows)
                {
                    var emb = TryDeserialize(f.FaceEncoding);
                    if (emb == null || emb.Length != _engine.EmbeddingSize)
                    {
                        skipped++;
                        continue;
                    }

                    if (f.StudentId.HasValue && f.Student != null && f.Student.FaceRecognitionEnabled
                        && f.Student.Status != StudentStatus.Blocked && f.Student.Status != StudentStatus.Archived)
                    {
                        list.Add(new KnownFace("Student", f.Student.Id, f.Student.FullName, emb));
                    }
                    else if (f.StaffId.HasValue && f.Staff != null && f.Staff.FaceRecognitionEnabled
                        && f.Staff.Status == StaffStatus.Active)
                    {
                        list.Add(new KnownFace("Staff", f.Staff.Id, f.Staff.FullName, emb));
                    }
                    else if (f.TeacherId.HasValue && f.Teacher != null && f.Teacher.FaceRecognitionEnabled
                        && f.Teacher.Status == TeacherStatus.Active)
                    {
                        list.Add(new KnownFace("Teacher", f.Teacher.Id, f.Teacher.FullName, emb));
                    }
                }

                _cache = list;
                LastReloadAt = DateTime.UtcNow;
                _logger.LogInformation("KnownFaceCache yangilandi: {Count} ta yuz, {Skipped} ta o'tkazib yuborildi", list.Count, skipped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KnownFaceCache yangilashda xato");
            }
            finally
            {
                try { _reloadGate.Release(); } catch (ObjectDisposedException) { }
            }
        }

        private static float[]? TryDeserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<float[]>(json); }
            catch { return null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 1) Avval timer'ni to'xtatamiz — yangi reload boshlanmaydi.
            _timer.Dispose();

            // 2) Ketayotgan reload tugashini kutamiz (gate'ni egallash orqali).
            try
            {
                if (_reloadGate.Wait(TimeSpan.FromSeconds(10)))
                    _reloadGate.Release();
            }
            catch (ObjectDisposedException) { }

            // 3) _reloadGate ATAYIN dispose qilinmaydi — kech kelgan ReloadAsync
            //    chaqiruvi ObjectDisposedException bilan yiqilmasligi uchun.
            //    (_disposed bayrog'i yangi ishni boshlashiga to'sqinlik qiladi.)
        }
    }
}
