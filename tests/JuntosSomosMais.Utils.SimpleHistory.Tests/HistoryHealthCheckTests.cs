using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class HistoryHealthCheckTests
{
    private static readonly DateTime Start = new(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly HistoryOptions _orders = new("myOrders") { UnhealthyAfter = TimeSpan.FromMinutes(5) };
    private readonly HistoryOptions _carts = new("carts") { UnhealthyAfter = TimeSpan.FromMinutes(5) };
    private readonly HistoryHealthState _ordersState;
    private readonly HistoryHealthState _cartsState;

    public HistoryHealthCheckTests()
    {
        _ordersState = new HistoryHealthState(_clock);
        _cartsState = new HistoryHealthState(_clock);
    }

    private HistoryHealthRegistry TwoSources() => new(new[] { (_orders, _ordersState), (_carts, _cartsState) });

    private Task<HealthCheckResult> CheckAsync(HistoryHealthRegistry registry, HealthStatus failureStatus = HealthStatus.Unhealthy)
    {
        var sut = new HistoryHealthCheck(registry, _clock);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("simplehistory", sut, failureStatus, tags: null),
        };
        return sut.CheckHealthAsync(context);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenEverySourceIsFresh_ShouldBeHealthyWithOneDataEntryPerSource()
    {
        _clock.Advance(TimeSpan.FromMinutes(5)); // exactly the limit is still fine

        var result = await CheckAsync(TwoSources());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("0:05:00 since start, no checkpoint yet", result.Data["myOrders"]);
        Assert.Equal("0:05:00 since start, no checkpoint yet", result.Data["carts"]);
    }

    [Theory]
    [InlineData(HealthStatus.Unhealthy)]
    [InlineData(HealthStatus.Degraded)]
    public async Task CheckHealthAsync_WhenOneOfTwoSourcesIsStale_ShouldReportTheFailureStatusAndNameOnlyTheStaleOne(HealthStatus failureStatus)
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        _ordersState.MarkFailure(new TimeoutException("mongo-0.internal:27017 timed out"));
        _clock.Advance(TimeSpan.FromSeconds(270));
        _cartsState.MarkCheckpoint();
        _clock.Advance(TimeSpan.FromSeconds(30));

        var result = await CheckAsync(TwoSources(), failureStatus);

        Assert.Equal(failureStatus, result.Status);
        Assert.Equal(
            "myOrders: no checkpoint for 0:06:00 (since start, no checkpoint yet); last error: TimeoutException (at 2026-10-06T10:01:00.0000000Z)",
            result.Description);
        Assert.DoesNotContain("carts", result.Description);
        Assert.DoesNotContain("mongo-0.internal", result.Description);
        Assert.Equal(new[] { "carts", "myOrders" }, result.Data.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("0:00:30 since the last checkpoint", result.Data["carts"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenBothSourcesAreStale_ShouldListBothWithoutAnErrorWhenNoneHappened()
    {
        _clock.Advance(TimeSpan.FromMinutes(6));

        var result = await CheckAsync(TwoSources());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(
            "myOrders: no checkpoint for 0:06:00 (since start, no checkpoint yet); last error: none"
            + " | carts: no checkpoint for 0:06:00 (since start, no checkpoint yet); last error: none",
            result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WithNoSources_ShouldBeHealthy()
    {
        var result = await CheckAsync(new HistoryHealthRegistry(Array.Empty<(HistoryOptions, HistoryHealthState)>()));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenARecentCheckpointFollowsAnOldOne_ShouldCountFromTheRecentOne()
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        _ordersState.MarkCheckpoint();
        _cartsState.MarkCheckpoint();
        _clock.Advance(TimeSpan.FromHours(3));
        Assert.Equal(HealthStatus.Unhealthy, (await CheckAsync(TwoSources())).Status);

        _cartsState.MarkCheckpoint();
        _clock.Advance(TimeSpan.FromSeconds(10));
        _ordersState.MarkCheckpoint();
        _clock.Advance(TimeSpan.FromSeconds(10));
        var result = await CheckAsync(TwoSources());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("0:00:10 since the last checkpoint", result.Data["myOrders"]);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldHonourEachSourcesOwnLimit()
    {
        _carts.UnhealthyAfter = TimeSpan.FromSeconds(10);
        _clock.Advance(TimeSpan.FromSeconds(11));

        var result = await CheckAsync(TwoSources());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.StartsWith("carts: no checkpoint for", result.Description);
    }
}
