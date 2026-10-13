namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// In-process health of ONE tracked source, written by the processor and the hosted service and read by
/// <see cref="HistoryHealthCheck"/>. Thread-safe: writers and readers run on different threads.
/// </summary>
public sealed class HistoryHealthState
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private DateTime? _lastCheckpointUtc;
    private string? _lastError;
    private DateTime? _lastErrorUtc;
    private HistoryGap? _lastGap;

    /// <param name="timeProvider">
    /// Clock used for <see cref="StartedAtUtc"/> and every mark; defaults to the system clock. Give
    /// <see cref="HistoryHealthCheck"/> the same clock.
    /// </param>
    public HistoryHealthState(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        StartedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>When this state was created.</summary>
    public DateTime StartedAtUtc { get; }

    /// <summary>When the resume token was last saved, or null when it never was.</summary>
    public DateTime? LastCheckpointUtc
    {
        get
        {
            lock (_gate)
                return _lastCheckpointUtc;
        }
    }

    /// <summary>Exception type name of the last failure of the loop, or null when it never failed.</summary>
    public string? LastError
    {
        get
        {
            lock (_gate)
                return _lastError;
        }
    }

    /// <summary>When the last failure happened, or null when it never failed.</summary>
    public DateTime? LastErrorUtc
    {
        get
        {
            lock (_gate)
                return _lastErrorUtc;
        }
    }

    /// <summary>The last window whose changes were not recorded, or null when there was none.</summary>
    public HistoryGap? LastGap
    {
        get
        {
            lock (_gate)
                return _lastGap;
        }
    }

    /// <summary>The checkpoint, failure and gap fields read together under one lock, so they describe the same moment.</summary>
    public HistoryHealthSnapshot Snapshot()
    {
        lock (_gate)
            return new HistoryHealthSnapshot(_lastCheckpointUtc, _lastError, _lastErrorUtc, _lastGap);
    }

    /// <summary>Records a checkpoint saved now.</summary>
    public void MarkCheckpoint()
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        lock (_gate)
            _lastCheckpointUtc = utcNow;
    }

    /// <summary>
    /// Records a window whose changes were not recorded. A null <paramref name="toUtc"/> means it ended now. The latest
    /// gap wins: an older one never replaces it.
    /// </summary>
    /// <param name="fromUtc">When the last checkpoint before the gap was saved, or null when unknown.</param>
    /// <param name="toUtc">When the stream restarted from now; null means now.</param>
    public void MarkGap(DateTime? fromUtc, DateTime? toUtc)
    {
        var gap = new HistoryGap(fromUtc, toUtc ?? _timeProvider.GetUtcNow().UtcDateTime);
        lock (_gate)
        {
            if (_lastGap is null || gap.ToUtc >= _lastGap.ToUtc)
                _lastGap = gap;
        }
    }

    /// <summary>Records a failure of the loop that happened now (only the exception type name is kept).</summary>
    public void MarkFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            _lastError = exception.GetType().Name;
            _lastErrorUtc = utcNow;
        }
    }
}
