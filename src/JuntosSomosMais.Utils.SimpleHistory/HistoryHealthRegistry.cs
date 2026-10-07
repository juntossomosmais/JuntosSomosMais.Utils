namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// The health state of every tracked source, registered as a singleton by the provider (for example
/// <c>AddMongoHistory</c>) and read by <see cref="HistoryHealthCheck"/>.
/// </summary>
public sealed class HistoryHealthRegistry
{
    /// <param name="sources">Options and health state of each tracked source, in registration order.</param>
    public HistoryHealthRegistry(IEnumerable<(HistoryOptions Options, HistoryHealthState State)> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        Sources = sources.ToList();
    }

    /// <summary>One entry per tracked source: its options (name, <c>UnhealthyAfter</c>) and its live health state.</summary>
    public IReadOnlyList<(HistoryOptions Options, HistoryHealthState State)> Sources { get; }
}
