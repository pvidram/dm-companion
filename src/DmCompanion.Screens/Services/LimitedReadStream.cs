namespace DmCompanion.Screens.Services;

/// <summary>Flujo de solo lectura que expone <paramref name="length"/> bytes de otro flujo desde <paramref name="start"/>.</summary>
public sealed class LimitedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start;
    private readonly long _length;
    private long _position;
    private bool _positioned;

    public LimitedReadStream(Stream inner, long start, long length)
    {
        _inner = inner;
        _start = start;
        _length = length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var take = Prepare(buffer.Length);
        if (take == 0) return 0;
        var read = _inner.Read(buffer[..take]);
        _position += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_positioned) await PositionAsync(cancellationToken);
        var take = Prepare(buffer.Length);
        if (take == 0) return 0;
        var read = await _inner.ReadAsync(buffer[..take], cancellationToken);
        _position += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int Prepare(int requested)
    {
        if (!_positioned) Position0();
        return (int)Math.Min(requested, _length - _position);
    }

    private void Position0()
    {
        _positioned = true;
        if (_inner.CanSeek) { _inner.Seek(_start, SeekOrigin.Begin); return; }
        var scratch = new byte[8192];
        for (var left = _start; left > 0;)
        {
            var read = _inner.Read(scratch, 0, (int)Math.Min(scratch.Length, left));
            if (read == 0) return;
            left -= read;
        }
    }

    private async Task PositionAsync(CancellationToken ct)
    {
        _positioned = true;
        if (_inner.CanSeek) { _inner.Seek(_start, SeekOrigin.Begin); return; }
        var scratch = new byte[8192];
        for (var left = _start; left > 0;)
        {
            var read = await _inner.ReadAsync(scratch.AsMemory(0, (int)Math.Min(scratch.Length, left)), ct);
            if (read == 0) return;
            left -= read;
        }
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        // Stream.Dispose() ya libera el flujo interno; no se libera dos veces.
        Dispose();
        return ValueTask.CompletedTask;
    }
}
