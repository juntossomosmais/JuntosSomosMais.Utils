namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class HistoryHealthStateTests
{
    private static readonly DateTime Start = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ShouldTakeStartFromTheClockAndHaveNoCheckpointOrError()
    {
        var sut = new HistoryHealthState(new FakeTimeProvider(Start));

        Assert.Equal(Start, sut.StartedAtUtc);
        Assert.Null(sut.LastCheckpointUtc);
        Assert.Null(sut.LastError);
        Assert.Null(sut.LastErrorUtc);
        Assert.Null(sut.LastGap);
    }

    [Fact]
    public void MarkGap_WithoutAnEnd_ShouldEndItAtTheStatesClockAndShowInTheSnapshot()
    {
        var clock = new FakeTimeProvider(Start);
        var sut = new HistoryHealthState(clock);
        clock.Advance(TimeSpan.FromMinutes(3));

        sut.MarkGap(Start.AddHours(-2), null);

        Assert.Equal(new HistoryGap(Start.AddHours(-2), Start.AddMinutes(3)), sut.LastGap);
        Assert.Equal(sut.LastGap, sut.Snapshot().LastGap);
    }

    [Fact]
    public void MarkGap_WithAnOlderGap_ShouldKeepTheLatestOne()
    {
        var sut = new HistoryHealthState(new FakeTimeProvider(Start));
        sut.MarkGap(null, Start.AddHours(-1));

        sut.MarkGap(Start.AddHours(-5), Start.AddHours(-3));

        Assert.Equal(new HistoryGap(null, Start.AddHours(-1)), sut.LastGap);
    }

    [Fact]
    public void MarkCheckpoint_ShouldKeepTheLatestTimeOfTheStatesClock()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var sut = new HistoryHealthState(clock);

        // Act
        clock.Advance(TimeSpan.FromSeconds(5));
        sut.MarkCheckpoint();
        clock.Advance(TimeSpan.FromSeconds(4));
        sut.MarkCheckpoint();

        // Assert
        Assert.Equal(Start.AddSeconds(9), sut.LastCheckpointUtc);
    }

    [Fact]
    public void MarkFailure_ShouldKeepOnlyTheTypeNameAndTimeOfTheLatestFailure()
    {
        // Arrange
        var clock = new FakeTimeProvider(Start);
        var sut = new HistoryHealthState(clock);

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        sut.MarkFailure(new InvalidOperationException("first"));
        clock.Advance(TimeSpan.FromSeconds(1));
        sut.MarkFailure(new TimeoutException("mongo-0.internal:27017 timed out"));

        // Assert

        Assert.Equal("TimeoutException", sut.LastError);
        Assert.Equal(Start.AddSeconds(2), sut.LastErrorUtc);
        Assert.Null(sut.LastCheckpointUtc);
    }

    [Fact]
    public void Snapshot_ShouldReturnTheCurrentValuesTogether()
    {
        var clock = new FakeTimeProvider(Start);
        var sut = new HistoryHealthState(clock);
        clock.Advance(TimeSpan.FromSeconds(3));
        sut.MarkCheckpoint();
        clock.Advance(TimeSpan.FromSeconds(1));
        sut.MarkFailure(new TimeoutException("x"));

        var snapshot = sut.Snapshot();

        Assert.Equal(new HistoryHealthSnapshot(Start.AddSeconds(3), "TimeoutException", Start.AddSeconds(4)), snapshot);
    }

    [Fact]
    public void Snapshot_WhenNothingHappened_ShouldBeEmpty()
    {
        Assert.Equal(new HistoryHealthSnapshot(null, null, null), new HistoryHealthState(new FakeTimeProvider(Start)).Snapshot());
    }

    [Fact]
    public void MarkFailure_WithNullException_ShouldThrow()
    {
        // Arrange
        var sut = new HistoryHealthState(new FakeTimeProvider(Start));

        // Act
        var ex = Record.Exception(() => sut.MarkFailure(null!)); // null on purpose: the guard is under test

        // Assert
        Assert.IsType<ArgumentNullException>(ex);
        Assert.Null(sut.LastError);
    }
}
