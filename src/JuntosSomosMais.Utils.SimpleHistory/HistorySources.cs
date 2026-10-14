namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>The immutable list of tracked collections, e.g. to run <c>MongoHistorySetup.EnsureAsync</c> from a tool.</summary>
public sealed class HistorySources
{
    /// <param name="options">Options of every tracked collection.</param>
    public HistorySources(IReadOnlyList<HistoryOptions> options)
    {
        Options = options;
    }

    /// <summary>One entry per tracked collection, in registration order.</summary>
    public IReadOnlyList<HistoryOptions> Options { get; }
}
