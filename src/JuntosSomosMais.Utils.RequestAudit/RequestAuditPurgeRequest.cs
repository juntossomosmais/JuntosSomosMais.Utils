namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Which audit entries to delete: <c>FromUtc &lt;= CreatedAtUtc &lt; ToUtc</c>.</summary>
/// <param name="FromUtc">Inclusive lower bound on <c>CreatedAtUtc</c>; open when null.</param>
/// <param name="ToUtc">
/// Exclusive upper bound on <c>CreatedAtUtc</c>. When null the purger pins it to the time the purge starts, so it
/// never chases entries written while it runs.
/// </param>
/// <param name="DryRun">Only count the matching entries, delete nothing.</param>
/// <param name="BatchSize">Entries deleted per round trip, so a large purge never holds one long-running delete.</param>
public sealed record RequestAuditPurgeRequest(DateTime? FromUtc, DateTime? ToUtc, bool DryRun, int BatchSize = 5000);
