namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>A consistent read of <see cref="HistoryHealthState"/>.</summary>
/// <param name="LastCheckpointUtc">When the resume token was last saved, or null when it never was.</param>
/// <param name="LastError">Exception type name of the last failure, or null when the loop never failed.</param>
/// <param name="LastErrorUtc">When the last failure happened, or null when the loop never failed.</param>
/// <param name="LastGap">The last window whose changes were not recorded, or null when there was none.</param>
public readonly record struct HistoryHealthSnapshot(
    DateTime? LastCheckpointUtc, string? LastError, DateTime? LastErrorUtc, HistoryGap? LastGap = null);
