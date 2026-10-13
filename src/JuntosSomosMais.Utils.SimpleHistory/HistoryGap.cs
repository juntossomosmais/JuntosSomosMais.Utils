namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>
/// A window whose changes were not recorded: the saved position fell out of the change feed, so the stream restarted
/// from now. Changes between <paramref name="FromUtc"/> and <paramref name="ToUtc"/> must be reconciled by hand.
/// </summary>
/// <param name="FromUtc">When the last checkpoint before the gap was saved, or null when unknown.</param>
/// <param name="ToUtc">When the stream restarted from now.</param>
public sealed record HistoryGap(DateTime? FromUtc, DateTime ToUtc);
