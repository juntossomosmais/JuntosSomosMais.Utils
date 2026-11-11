namespace JuntosSomosMais.Utils.Shared;

/// <summary>
/// Validates the interval and batch size of a purge request. Linked as source into each provider package that needs
/// it, so every assembly has its own internal copy.
/// </summary>
internal static class PurgeInterval
{
    private const string RequestParam = "request";

    /// <summary>Converts both bounds to UTC, checks them and pins an open upper bound to the current time.</summary>
    /// <param name="fromUtc">Inclusive lower bound, if any.</param>
    /// <param name="toUtc">Exclusive upper bound, if any.</param>
    /// <param name="batchSize">How many documents are deleted per round trip.</param>
    /// <param name="timeProvider">Clock pinning an open upper bound; defaults to the system clock.</param>
    /// <param name="purgedNoun">What is purged ("entry", "row"), named in the error for a request without bounds.</param>
    /// <returns>The lower bound in UTC, if any, and the upper bound the purge stops at.</returns>
    /// <exception cref="ArgumentException">No bound, an unspecified DateTimeKind, a non-positive batch size or an empty or inverted interval.</exception>
    public static (DateTime? FromUtc, DateTime PinnedToUtc) Resolve(
        DateTime? fromUtc, DateTime? toUtc, int batchSize, TimeProvider? timeProvider, string purgedNoun)
    {
        var from = ToUtc(fromUtc, "FromUtc");
        var to = ToUtc(toUtc, "ToUtc");
        if (from is null && to is null)
            throw new ArgumentException(
                $"At least one of FromUtc or ToUtc is required: the purger never deletes every {purgedNoun}.", RequestParam);
        if (batchSize <= 0)
            throw new ArgumentException("BatchSize must be positive.", RequestParam);

        var pinnedTo = to ?? (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (from >= pinnedTo)
            throw new ArgumentException(
                to is null ? "FromUtc must be before the start of the purge." : "FromUtc must be before ToUtc.", RequestParam);

        return (from, pinnedTo);
    }

    private static DateTime? ToUtc(DateTime? value, string name) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => throw new ArgumentException($"{name} must have DateTimeKind Utc or Local, not Unspecified.", name),
    };
}
