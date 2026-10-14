namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Which history rows to delete: <c>FromUtc &lt;= HistoryDate &lt; ToUtc</c>.</summary>
/// <param name="FromUtc">Inclusive lower bound on <c>HistoryDate</c>; open when null.</param>
/// <param name="ToUtc">
/// Exclusive upper bound on <c>HistoryDate</c>. When null the purger pins it to the time the purge starts, so it
/// never chases rows written while it runs.
/// </param>
/// <param name="DryRun">Only count the matching rows, delete nothing.</param>
/// <param name="BatchSize">Rows deleted per round trip, so a large purge never holds one long-running delete.</param>
public sealed record HistoryPurgeRequest(DateTime? FromUtc, DateTime? ToUtc, bool DryRun, int BatchSize = 5000);
