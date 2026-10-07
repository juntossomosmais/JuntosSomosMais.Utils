namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Fluent description of the tracked MongoDB collections.</summary>
public sealed class MongoHistoryBuilder : HistoryBuilder<MongoHistoryOptions>
{
    /// <inheritdoc />
    protected override MongoHistoryOptions CreateOptions(string sourceName) => new(sourceName);

    /// <summary>Tracks <paramref name="sourceCollection"/> with its own history collection and options.</summary>
    /// <param name="sourceCollection">Collection whose changes are recorded.</param>
    /// <param name="configure">Optional per-collection options.</param>
    /// <exception cref="InvalidOperationException">The collection is already tracked.</exception>
    public new MongoHistoryBuilder Track(string sourceCollection, Action<MongoHistoryOptions>? configure = null)
    {
        base.Track(sourceCollection, configure);
        return this;
    }
}
