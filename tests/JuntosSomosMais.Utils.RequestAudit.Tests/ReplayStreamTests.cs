using System.Runtime.ExceptionServices;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class ReplayStreamTests
{
    [Fact]
    public void Read_HeadThenRest_ReturnsEveryByteInOrder()
    {
        // Arrange
        using var rest = new MemoryStream("789"u8.ToArray());
        using var stream = new ReplayStream("0123456"u8.ToArray(), rest, failure: null);
        var result = new List<byte>();
        var buffer = new byte[4];

        // Act
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            result.AddRange(buffer.Take(count));

        // Assert
        Assert.Equal("0123456789", System.Text.Encoding.ASCII.GetString(result.ToArray()));
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_HeadThenFailure_ThrowsTheSameExceptionAfterTheHead()
    {
        // Arrange
        var failure = new IOException("reset");
        using var rest = new MemoryStream();
        using var stream = new ReplayStream("ab"u8.ToArray(), rest, ExceptionDispatchInfo.Capture(failure));
        var buffer = new byte[8];

        // Act
        var head = await stream.ReadAsync(buffer);
        var caught = await Assert.ThrowsAsync<IOException>(async () => await stream.ReadExactlyAsync(buffer));

        // Assert
        Assert.Equal(2, head);
        Assert.Same(failure, caught);
    }

    [Fact]
    public void Members_Unsupported_Throw()
    {
        // Arrange
        using var stream = new ReplayStream(ReadOnlyMemory<byte>.Empty, Stream.Null, failure: null);

        // Act
        stream.Flush();

        // Assert
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
    }
}
