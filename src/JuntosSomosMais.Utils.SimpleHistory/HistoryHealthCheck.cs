using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// One check for every tracked source. A source is stale when it saved no checkpoint for longer than its
/// <see cref="HistoryOptions.UnhealthyAfter"/> (counted from the process start until the first checkpoint); any stale
/// source makes the check report the registration's failure status. Covers a setup that keeps failing, a stream that
/// cannot be opened and a hung cursor, without a database round trip. Otherwise, a source whose last gap ended less
/// than <see cref="HistoryOptions.GapReportedFor"/> ago makes the check report <see cref="HealthStatus.Degraded"/>,
/// never a failure: the stream is running again and a restart would not bring the lost changes back. Register it with
/// <see cref="HealthChecksBuilderExtensions.AddSimpleHistoryHealthCheck"/>.
/// </summary>
internal sealed class HistoryHealthCheck : IHealthCheck
{
    private readonly HistoryHealthRegistry _registry;
    private readonly TimeProvider _timeProvider;

    /// <param name="registry">Options and health state of every tracked source.</param>
    public HistoryHealthCheck(HistoryHealthRegistry registry)
        : this(registry, TimeProvider.System)
    {
    }

    internal HistoryHealthCheck(HistoryHealthRegistry registry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _registry = registry;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var stale = new List<string>();
        var gaps = new List<string>();

        foreach (var (options, state) in _registry.Sources)
        {
            var snapshot = state.Snapshot();
            var age = now - (snapshot.LastCheckpointUtc ?? state.StartedAtUtc);
            var since = snapshot.LastCheckpointUtc.HasValue ? "since the last checkpoint" : "since start, no checkpoint yet";
            data[options.SourceName] = $"{age:g} {since}";

            if (age > options.UnhealthyAfter)
            {
                var lastError = snapshot.LastError is null ? "none" : $"{snapshot.LastError} (at {snapshot.LastErrorUtc:O})";
                stale.Add($"{options.SourceName}: no checkpoint for {age:g} ({since}); last error: {lastError}");
            }

            if (snapshot.LastGap is { } gap && now - gap.ToUtc < options.GapReportedFor)
            {
                var from = gap.FromUtc.HasValue ? $"{gap.FromUtc:O}" : "an unknown time";
                gaps.Add($"{options.SourceName}: changes between {from} and {gap.ToUtc:O} were not recorded");
            }
        }

        if (stale.Count > 0)
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus, string.Join(" | ", stale.Concat(gaps)), data: data));

        return Task.FromResult(gaps.Count > 0
            ? HealthCheckResult.Degraded(string.Join(" | ", gaps), data: data)
            : HealthCheckResult.Healthy(data: data));
    }
}
