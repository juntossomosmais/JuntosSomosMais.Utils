namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Options of ONE tracked MongoDB collection: the neutral options plus the change stream settings.</summary>
public sealed class MongoHistoryOptions : HistoryOptions
{
    /// <param name="sourceCollection">Name of the collection whose changes are recorded.</param>
    public MongoHistoryOptions(string sourceCollection)
        : base(sourceCollection)
    {
    }

    /// <summary>Creates the history collection with <c>block_compressor=zstd</c>.</summary>
    public bool CompressHistory { get; set; } = true;

    /// <summary>
    /// Server-side wait of one change stream batch. A checkpoint is saved after every batch, so it must be shorter
    /// than <see cref="HistoryOptions.UnhealthyAfter"/>.
    /// </summary>
    public TimeSpan MaxAwaitTime { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Adds fields that never count as a change.</summary>
    public new MongoHistoryOptions IgnoreFields(params string[] names)
    {
        base.IgnoreFields(names);
        return this;
    }

    /// <summary>Restricts the stored fields to <paramref name="names"/> (every field when never called).</summary>
    public new MongoHistoryOptions TrackFields(params string[] names)
    {
        base.TrackFields(names);
        return this;
    }

    /// <inheritdoc />
    public override void Validate()
    {
        base.Validate();
        if (MaxAwaitTime <= TimeSpan.Zero)
            throw new InvalidOperationException($"{SourceName}: MaxAwaitTime must be positive.");
        if (UnhealthyAfter <= MaxAwaitTime)
            throw new InvalidOperationException($"{SourceName}: UnhealthyAfter must be longer than MaxAwaitTime.");
    }
}
