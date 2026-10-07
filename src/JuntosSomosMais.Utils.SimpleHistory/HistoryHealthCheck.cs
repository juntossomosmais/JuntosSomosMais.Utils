using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// One check for every tracked source. A source is stale when it saved no checkpoint for longer than its
/// <see cref="HistoryOptions.UnhealthyAfter"/> (counted from the process start until the first checkpoint); any stale
/// source makes the check report the registration's failure status. Covers a setup that keeps failing, a stream that
/// cannot be opened and a hung cursor, without a database round trip. Register it with
/// <see cref="HealthChecksBuilderExtensions.AddSimpleHistoryHealthCheck"/>.
/// </summary>
public sealed class HistoryHealthCheck : IHealthCheck
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
        }

        return Task.FromResult(stale.Count == 0
            ? HealthCheckResult.Healthy(data: data)
            : new HealthCheckResult(context.Registration.FailureStatus, string.Join(" | ", stale), data: data));
    }
}
