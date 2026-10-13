namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// Provider-neutral options of ONE tracked source (a collection, a table...). Every tracked source has its own
/// instance. Providers derive from it to add their own settings.
/// </summary>
public class HistoryOptions
{
    private string? _historyName;

    /// <param name="sourceName">Name of the source whose changes are recorded.</param>
    public HistoryOptions(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("The source name is required.", nameof(sourceName));
        SourceName = sourceName;
    }

    /// <summary>Source whose changes are recorded.</summary>
    public string SourceName { get; }

    /// <summary>Store that receives the history rows. Defaults to <c>{SourceName}History</c>.</summary>
    public string HistoryName
    {
        get => _historyName ?? SourceName + "History";
        set => _historyName = value;
    }

    /// <summary>Store holding the resume position, one entry per source. Shared by all sources.</summary>
    public string CheckpointName { get; set; } = "simpleHistoryCheckpoint";

    /// <summary>Unique id of this source's resume-position entry.</summary>
    public string CheckpointId => SourceName;

    /// <summary>Top-level fields that never count as a change (case sensitive). They are still stored in full rows.</summary>
    public ISet<string> IgnoredFields { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Top-level fields kept in every row's <c>Data</c> (case sensitive). Empty means every field. A change that
    /// touches only untracked fields records nothing.
    /// </summary>
    public ISet<string> TrackedFields { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// When true a modification stores the whole document (filtered by <see cref="TrackedFields"/>) in a <c>~</c> row
    /// whose <c>ChangedFields</c> names what changed, instead of only the changed fields. Default: delta rows.
    /// </summary>
    public bool StoreFullDocument { get; set; }

    /// <summary>Number of changes read from the source per batch.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>First retry delay of the hosted service after a failure.</summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Upper bound of the exponential retry delay.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long the source may go without saving a checkpoint before <see cref="HistoryHealthCheck"/> reports it
    /// unhealthy. Counted from the process start until the first checkpoint. Must be longer than
    /// <see cref="MaxBackoff"/>, so one retry wait alone never turns the check red.
    /// </summary>
    public TimeSpan UnhealthyAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long <see cref="HistoryHealthCheck"/> reports the source as degraded after a gap (a window whose changes
    /// were not recorded), counted from the end of the gap.
    /// </summary>
    public TimeSpan GapReportedFor { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Adds fields that never count as a change.</summary>
    public HistoryOptions IgnoreFields(params string[] names)
    {
        foreach (var name in names)
            IgnoredFields.Add(name);
        return this;
    }

    /// <summary>Restricts the stored fields to <paramref name="names"/> (every field when never called).</summary>
    public HistoryOptions TrackFields(params string[] names)
    {
        foreach (var name in names)
            TrackedFields.Add(name);
        return this;
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> when the options are inconsistent.</summary>
    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(HistoryName))
            throw new InvalidOperationException($"{SourceName}: the history name is required.");
        if (HistoryName == SourceName)
            throw new InvalidOperationException($"{SourceName}: the history name must differ from the source name.");
        if (string.IsNullOrWhiteSpace(CheckpointName))
            throw new InvalidOperationException($"{SourceName}: the checkpoint name is required.");
        if (CheckpointName == SourceName || CheckpointName == HistoryName)
            throw new InvalidOperationException($"{SourceName}: the checkpoint name must differ from the source and history names.");
        if (BatchSize <= 0)
            throw new InvalidOperationException($"{SourceName}: BatchSize must be positive.");
        if (InitialBackoff <= TimeSpan.Zero)
            throw new InvalidOperationException($"{SourceName}: InitialBackoff must be positive.");
        if (MaxBackoff < InitialBackoff)
            throw new InvalidOperationException($"{SourceName}: MaxBackoff must not be shorter than InitialBackoff.");
        if (UnhealthyAfter <= TimeSpan.Zero)
            throw new InvalidOperationException($"{SourceName}: UnhealthyAfter must be positive.");
        if (UnhealthyAfter <= MaxBackoff)
            throw new InvalidOperationException($"{SourceName}: UnhealthyAfter must be longer than MaxBackoff.");
        if (GapReportedFor <= TimeSpan.Zero)
            throw new InvalidOperationException($"{SourceName}: GapReportedFor must be positive.");
        var nested = TrackedFields.Concat(IgnoredFields).FirstOrDefault(n => n.Contains('.') || n.StartsWith('$'));
        if (nested is not null)
            throw new InvalidOperationException($"{SourceName}: {nested} is not a top-level field name.");
        if (IgnoredFields.Contains("_id") || TrackedFields.Contains("_id"))
            throw new InvalidOperationException($"{SourceName}: _id is the entity id and cannot be ignored or tracked.");
        var conflict = TrackedFields.FirstOrDefault(IgnoredFields.Contains);
        if (conflict is not null)
            throw new InvalidOperationException($"{SourceName}: {conflict} cannot be both ignored and tracked.");
    }
}
