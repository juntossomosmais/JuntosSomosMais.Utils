namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>
/// Write-only stream that sends everything to <paramref name="inner"/> and keeps a copy of the first
/// <paramref name="limit"/> bytes. It never buffers: what it captured is what was written to the response.
/// </summary>
internal sealed class CapturingStream(Stream inner, int limit) : Stream
{
    private readonly MemoryStream _captured = new();
    private long _total;

    /// <summary>More than the limit was written, so <see cref="Captured"/> is incomplete.</summary>
    public bool Truncated => _total > limit;

    /// <summary>The first bytes written, up to the limit.</summary>
    public byte[] Captured => _captured.ToArray();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        Capture(buffer.AsSpan(offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Capture(buffer);
        inner.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Capture(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _captured.Dispose();

        base.Dispose(disposing);
    }

    private void Capture(ReadOnlySpan<byte> data)
    {
        _total += data.Length;
        var room = limit - (int)_captured.Length;
        if (room > 0)
            _captured.Write(data[..Math.Min(room, data.Length)]);
    }
}
