using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Opt-in registration of the SimpleHistory health check.</summary>
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="HistoryHealthCheck"/>, covering every tracked source. Needs the
    /// <see cref="HistoryHealthRegistry"/> the provider registers (for example <c>AddMongoHistory</c>).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Name of the check.</param>
    /// <param name="failureStatus">Status reported when a source is stale; null means <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="tags">Tags of the check; none by default.</param>
    public static IHealthChecksBuilder AddSimpleHistoryHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "simplehistory",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new HistoryHealthCheck(sp.GetRequiredService<HistoryHealthRegistry>()),
            failureStatus,
            tags ?? []));
    }
}
