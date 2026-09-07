using System.Diagnostics;

namespace SecureGate.Infrastructure.Services.Implementations;

/// <summary>
/// <c>ffmpeg</c> process'ining stdout'ini oddiy <see cref="Stream"/> sifatida ko'rsatadi va
/// stream dispose qilinganda process'ni (butun daraxti bilan) o'ldiradi.
///
/// <para>
/// <b>Nima uchun kerak:</b> <c>process.StandardOutput.BaseStream</c> ni to'g'ridan-to'g'ri qaytarish
/// xavfli — mijoz ulanishni uzganda ffmpeg "yetim" bo'lib qolib, RTSP oqimini cheksiz o'qiyveradi.
/// Bu wrapper <see cref="Dispose(bool)"/> da <c>Kill(entireProcessTree: true)</c> qiladi.
/// </para>
///
/// <para>
/// <b>stderr:</b> ffmpeg diagnostikani stderr'ga yozadi. Uni o'qimasak pipe buferi to'ladi va
/// process osilib qoladi (klassik tuzoq). Shu sababli chaqiruvchi <c>BeginErrorReadLine()</c> ni
/// yoqib, satrlarni logga yozishi shart — qarang <see cref="HikvisionNvrArchiveService"/>.
/// </para>
///
/// <para>
/// <b>Prefiks bufer:</b> process haqiqatdan ham ma'lumot berayotganini tekshirish uchun
/// birinchi baytlar oldindan o'qiladi. O'sha baytlar yo'qolmasligi uchun ular
/// <c>prefix</c> sifatida beriladi va birinchi navbatda qaytariladi.
/// </para>
/// </summary>
internal sealed class FfmpegProcessStream : Stream
{
    private readonly Process _process;
    private readonly Stream _inner;
    private readonly ILogger _logger;

    private byte[]? _prefix;
    private int _prefixOffset;
    private int _prefixLength;
    private bool _disposed;

    public FfmpegProcessStream(Process process, Stream inner, byte[]? prefix, int prefixLength, ILogger logger)
    {
        _process = process;
        _inner = inner;
        _logger = logger;
        _prefix = prefix;
        _prefixLength = prefixLength;
        _prefixOffset = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var taken = TakeFromPrefix(buffer.AsSpan(offset, count));
        if (taken > 0) return taken;
        return _inner.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var taken = TakeFromPrefix(buffer.Span);
        if (taken > 0) return taken;
        return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int TakeFromPrefix(Span<byte> destination)
    {
        if (_prefix is null) return 0;

        var available = _prefixLength - _prefixOffset;
        if (available <= 0)
        {
            _prefix = null;
            return 0;
        }

        var take = Math.Min(available, destination.Length);
        _prefix.AsSpan(_prefixOffset, take).CopyTo(destination);
        _prefixOffset += take;

        if (_prefixOffset >= _prefixLength) _prefix = null;
        return take;
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        _disposed = true;

        if (disposing)
        {
            KillProcess();

            try { _inner.Dispose(); } catch { /* pipe allaqachon yopilgan bo'lishi mumkin */ }
            try { _process.Dispose(); } catch { /* ignore */ }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }

    private void KillProcess()
    {
        try
        {
            if (!_process.HasExited)
            {
                // entireProcessTree: ffmpeg o'zi bola process yaratmaydi, lekin shell orqali
                // ishga tushirilgan holatlarda ham yetim qolmasligi kafolatlanadi.
                _process.Kill(entireProcessTree: true);

                // Kill asinxron — qisqa kutish resurslar bo'shashiga yordam beradi.
                if (!_process.WaitForExit(3000))
                    _logger.LogWarning("ffmpeg process (PID {Pid}) 3s ichida tugamadi", SafePid());
            }
        }
        catch (InvalidOperationException) { /* process allaqachon tugagan */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg process'ini to'xtatishda xato");
        }
    }

    private int SafePid()
    {
        try { return _process.Id; } catch { return -1; }
    }
}
