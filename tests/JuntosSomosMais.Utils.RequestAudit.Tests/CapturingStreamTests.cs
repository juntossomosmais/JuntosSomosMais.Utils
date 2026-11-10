using System.Text;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class CapturingStreamTests
{
    [Fact]
    public async Task WriteAsync_WithinLimit_WritesThroughAndCapturesEverything()
    {
        // Arrange
        using var inner = new MemoryStream();
        await using var stream = new CapturingStream(inner, limit: 10);

        // Act
        await stream.WriteAsync(Encoding.UTF8.GetBytes("hello"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("world"), 0, 5);
        await stream.FlushAsync();

        // Assert
        Assert.Equal("helloworld", Encoding.UTF8.GetString(inner.ToArray()));
        Assert.Equal("helloworld", Encoding.UTF8.GetString(stream.Captured));
        Assert.False(stream.Truncated);
    }

    [Fact]
    public void Write_PastLimit_WritesEverythingThroughAndCapturesUpToLimit()
    {
        // Arrange
        using var inner = new MemoryStream();
        using var stream = new CapturingStream(inner, limit: 7);

        // Act
        stream.Write(Encoding.UTF8.GetBytes("hello"), 0, 5);
        stream.Write(Encoding.UTF8.GetBytes("world").AsSpan());
        stream.Write(Encoding.UTF8.GetBytes("!"), 0, 1);
        stream.Flush();

        // Assert
        Assert.Equal("helloworld!", Encoding.UTF8.GetString(inner.ToArray()));
        Assert.Equal("hellowo", Encoding.UTF8.GetString(stream.Captured));
        Assert.True(stream.Truncated);
    }

    [Fact]
    public void Stream_IsWriteOnly()
    {
        // Arrange
        using var stream = new CapturingStream(new MemoryStream(), limit: 1);

        // Act
        var capabilities = (stream.CanRead, stream.CanSeek, stream.CanWrite);

        // Assert
        Assert.Equal((false, false, true), capabilities);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
    }

    [Fact]
    public void Dispose_LeavesTheInnerStreamOpen()
    {
        // Arrange
        using var inner = new MemoryStream();
        var stream = new CapturingStream(inner, limit: 1);

        // Act
        stream.Dispose();

        // Assert
        Assert.True(inner.CanWrite);
    }
}
