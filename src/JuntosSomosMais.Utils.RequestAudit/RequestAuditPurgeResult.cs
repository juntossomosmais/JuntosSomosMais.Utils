namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Outcome of a purge.</summary>
/// <param name="Matched">Entries inside the requested interval.</param>
/// <param name="Deleted">Entries actually deleted (always 0 on a dry run).</param>
/// <param name="DryRun">Whether this was a dry run.</param>
/// <param name="ToUtc">
/// The exclusive upper bound actually applied: the request's <c>ToUtc</c>, or the time the purge started when the
/// request left it open.
/// </param>
public sealed record RequestAuditPurgeResult(long Matched, long Deleted, bool DryRun, DateTime ToUtc);
