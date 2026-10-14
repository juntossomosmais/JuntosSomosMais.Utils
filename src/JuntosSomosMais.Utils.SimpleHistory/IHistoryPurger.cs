namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Deletes old rows from history collections (retention).</summary>
public interface IHistoryPurger
{
    /// <summary>
    /// Purges the history collection of every source, in input order, in batches of <c>request.BatchSize</c> rows.
    /// Never touches the source or the checkpoint collections. In delta mode, deleting the oldest rows of an entity
    /// makes a replay of its history fail; in full-document mode every remaining row is still a readable version.
    /// </summary>
    /// <exception cref="ArgumentException">No bound, an empty or inverted interval, an unspecified DateTimeKind or a non-positive batch size.</exception>
    Task<IReadOnlyList<HistoryPurgeResult>> PurgeAsync(
        IEnumerable<HistoryOptions> sources, HistoryPurgeRequest request, CancellationToken ct = default);
}
