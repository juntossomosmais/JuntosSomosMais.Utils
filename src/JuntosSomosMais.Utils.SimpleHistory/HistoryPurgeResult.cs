namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Outcome of the purge of ONE history collection.</summary>
/// <param name="HistoryName">The purged history collection.</param>
/// <param name="Matched">Rows inside the requested interval.</param>
/// <param name="Deleted">Rows actually deleted (always 0 on a dry run).</param>
/// <param name="DryRun">Whether nothing was deleted on purpose.</param>
/// <param name="ToUtc">
/// The exclusive upper bound actually applied: the request's <c>ToUtc</c>, or the time the purge started when the
/// request left it open (so rows written during the purge are never deleted). Null when the purger does not report it.
/// </param>
public sealed record HistoryPurgeResult(string HistoryName, long Matched, long Deleted, bool DryRun, DateTime? ToUtc = null);
