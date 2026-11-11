using JuntosSomosMais.Utils.Shared;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class PurgeIntervalTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_BothBounds_ReturnsThemInUtc()
    {
        // Arrange
        var from = Now.AddDays(-2).ToLocalTime();
        var to = Now.AddDays(-1);

        // Act
        var (fromUtc, pinnedTo) = PurgeInterval.Resolve(from, to, 10, new FakeTimeProvider(Now), "row", "request");

        // Assert
        Assert.Equal(Now.AddDays(-2), fromUtc);
        Assert.Equal(DateTimeKind.Utc, fromUtc!.Value.Kind);
        Assert.Equal(to, pinnedTo);
    }

    [Fact]
    public void Resolve_OnlyLowerBound_PinsTheUpperBoundToTheClock()
    {
        // Arrange
        var from = Now.AddDays(-1);

        // Act
        var (fromUtc, pinnedTo) = PurgeInterval.Resolve(from, null, 10, new FakeTimeProvider(Now), "row", "request");

        // Assert
        Assert.Equal(from, fromUtc);
        Assert.Equal(Now, pinnedTo);
    }

    [Fact]
    public void Resolve_OnlyUpperBound_KeepsItAndLeavesTheLowerBoundOpen()
    {
        // Arrange
        var to = Now.AddDays(-1);

        // Act
        var (fromUtc, pinnedTo) = PurgeInterval.Resolve(null, to, 10, new FakeTimeProvider(Now), "row", "request");

        // Assert
        Assert.Null(fromUtc);
        Assert.Equal(to, pinnedTo);
    }

    [Fact]
    public void Resolve_OnlyLowerBoundWithoutClock_PinsTheUpperBoundToTheSystemClock()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var (_, pinnedTo) = PurgeInterval.Resolve(before.AddDays(-1), null, 10, null, "row", "request");

        // Assert
        Assert.InRange(pinnedTo, before, DateTime.UtcNow);
    }

    [Theory]
    [InlineData("entry")]
    [InlineData("row")]
    public void Resolve_NoBound_NamesWhatIsPurgedInTheError(string noun)
    {
        // Arrange
        // Act
        var error = Assert.Throws<ArgumentException>(() => PurgeInterval.Resolve(null, null, 10, null, noun, "request"));

        // Assert
        Assert.Equal("request", error.ParamName);
        Assert.StartsWith($"At least one of FromUtc or ToUtc is required: the purger never deletes every {noun}.", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveBatchSize_Throws(int batchSize)
    {
        // Arrange
        // Act
        var error = Assert.Throws<ArgumentException>(() => PurgeInterval.Resolve(null, Now, batchSize, null, "row", "request"));

        // Assert
        Assert.Equal("request", error.ParamName);
        Assert.StartsWith("BatchSize must be positive.", error.Message);
    }

    [Fact]
    public void Resolve_UnspecifiedKind_NamesTheBound()
    {
        // Arrange
        var unspecified = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);

        // Act
        var fromError = Assert.Throws<ArgumentException>(() => PurgeInterval.Resolve(unspecified, null, 10, null, "row", "request"));
        var toError = Assert.Throws<ArgumentException>(() => PurgeInterval.Resolve(null, unspecified, 10, null, "row", "request"));

        // Assert
        Assert.Equal("FromUtc", fromError.ParamName);
        Assert.StartsWith("FromUtc must have DateTimeKind Utc or Local, not Unspecified.", fromError.Message);
        Assert.Equal("ToUtc", toError.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Resolve_LowerBoundNotBeforeTheUpperBound_Throws(int daysAfter)
    {
        // Arrange
        var to = Now.AddDays(-1);

        // Act
        var error = Assert.Throws<ArgumentException>(
            () => PurgeInterval.Resolve(to.AddDays(daysAfter), to, 10, new FakeTimeProvider(Now), "row", "request"));

        // Assert
        Assert.Equal("request", error.ParamName);
        Assert.StartsWith("FromUtc must be before ToUtc.", error.Message);
    }

    [Fact]
    public void Resolve_LowerBoundNotBeforeTheClock_Throws()
    {
        // Arrange
        // Act
        var error = Assert.Throws<ArgumentException>(
            () => PurgeInterval.Resolve(Now, null, 10, new FakeTimeProvider(Now), "row", "request"));

        // Assert
        Assert.StartsWith("FromUtc must be before the start of the purge.", error.Message);
    }
}
