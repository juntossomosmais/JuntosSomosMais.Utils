using System.Runtime.ExceptionServices;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>
/// Read-only stream that gives back bytes already read from <paramref name="rest"/>, then the failure that stopped
/// that read, or else the rest of the original stream. It never disposes <paramref name="rest"/>: the server owns it.
/// </summary>
internal sealed class ReplayStream(ReadOnlyMemory<byte> head, Stream rest, ExceptionDispatchInfo? failure) : Stream
{
    private int _served;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        // Read-only: nothing to flush.
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return 0;

        if (ReadHead(buffer) is var served and > 0)
            return served;

        failure?.Throw();
        return rest.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
            return ValueTask.FromResult(0);

        if (ReadHead(buffer.Span) is var served and > 0)
            return ValueTask.FromResult(served);

        failure?.Throw();
        return rest.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int ReadHead(Span<byte> buffer)
    {
        var count = Math.Min(buffer.Length, head.Length - _served);
        head.Span.Slice(_served, count).CopyTo(buffer);
        _served += count;
        return count;
    }
}
