namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// Fluent description of the tracked sources. Providers derive from it with their own options type and expose a
/// covariant <c>Track</c> so chaining keeps the derived builder.
/// </summary>
/// <typeparam name="TOptions">Options type of the provider.</typeparam>
public abstract class HistoryBuilder<TOptions>
    where TOptions : HistoryOptions
{
    private readonly List<TOptions> _sources = new();

    /// <summary>Creates the default options of one source.</summary>
    protected abstract TOptions CreateOptions(string sourceName);

    /// <summary>Tracks <paramref name="sourceName"/> with its own history store and options.</summary>
    /// <param name="sourceName">Source whose changes are recorded.</param>
    /// <param name="configure">Optional per-source options.</param>
    /// <exception cref="InvalidOperationException">The source is already tracked.</exception>
    public HistoryBuilder<TOptions> Track(string sourceName, Action<TOptions>? configure = null)
    {
        var options = CreateOptions(sourceName);
        if (_sources.Any(s => s.SourceName == sourceName))
            throw new InvalidOperationException($"{sourceName} is already tracked.");

        configure?.Invoke(options);
        _sources.Add(options);
        return this;
    }

    /// <summary>Validates every source and returns them. Throws <see cref="InvalidOperationException"/> when invalid.</summary>
    public IReadOnlyList<TOptions> Build()
    {
        if (_sources.Count == 0)
            throw new InvalidOperationException("No source is tracked: call Track at least once.");

        foreach (var source in _sources)
            source.Validate();

        var duplicated = _sources.GroupBy(s => s.HistoryName).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
            throw new InvalidOperationException($"The history store {duplicated.Key} is used by more than one tracked source.");

        // A history or checkpoint store that is itself tracked would feed its own change feed.
        var tracked = _sources.Select(s => s.SourceName).ToHashSet();
        var written = _sources.SelectMany(s => new[] { s.HistoryName, s.CheckpointName }).ToHashSet();
        var feedback = tracked.FirstOrDefault(written.Contains);
        if (feedback is not null)
            throw new InvalidOperationException($"{feedback} is tracked but also used as a history or checkpoint store.");

        var checkpointAsHistory = _sources.FirstOrDefault(s => _sources.Any(o => o.HistoryName == s.CheckpointName));
        return checkpointAsHistory is not null
            ? throw new InvalidOperationException($"{checkpointAsHistory.SourceName}: the checkpoint store is also a history store.")
            : (IReadOnlyList<TOptions>)_sources.ToList();
    }
}
