using MongoDB.Bson;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Pure: rebuilds a version of a tracked document (without <c>_id</c>) from its history rows.</summary>
public static class HistoryReplay
{
    /// <summary>
    /// Replays the rows. A chain that ends in a delete yields the last version before the delete; a chain that
    /// continues after a delete (the entity was re-inserted) yields the re-inserted document's version.
    /// </summary>
    /// <param name="rows">Rows of ONE entity, oldest first (sorted by <c>ChangeToken</c>).</param>
    /// <param name="upToChangeToken">When set, stops after applying the row with that <c>ChangeToken</c>.</param>
    /// <exception cref="InvalidOperationException">A delta follows no base row, or the chain has no base row.</exception>
    public static BsonDocument Rebuild(IEnumerable<HistoryEntry> rows, string? upToChangeToken = null)
    {
        BsonDocument? state = null;
        BsonDocument? lastDeleted = null;

        foreach (var row in rows)
        {
            if (row.HistoryType == HistoryTypes.Deleted)
            {
                // The next row, if any, is a re-insert: it starts a new chain.
                lastDeleted = VersionBeforeDelete(state, row) ?? lastDeleted;
                state = null;
            }
            else if (IsFullDocument(row))
            {
                state = row.Data.DeepClone().AsBsonDocument;
            }
            else if (row.HistoryType == HistoryTypes.Changed)
            {
                state = ApplyDelta(state, row);
            }

            if (upToChangeToken is not null && row.ChangeToken == upToChangeToken)
                break;
        }

        return state ?? lastDeleted ?? throw new InvalidOperationException("History chain has no base row ('+' or '=').");
    }

    private static bool IsFullDocument(HistoryEntry row) =>
        row.HistoryType is HistoryTypes.Created or HistoryTypes.Snapshot
        || (row.HistoryType == HistoryTypes.Changed && row.ChangedFields is not null);

    private static BsonDocument ApplyDelta(BsonDocument? state, HistoryEntry row)
    {
        if (state is null)
            throw new InvalidOperationException("History chain has no base row ('+' or '=') before a delta.");
        foreach (var element in row.Data)
            state[element.Name] = element.Value.DeepClone();
        foreach (var name in row.RemovedFields ?? [])
            state.Remove(name);
        return state;
    }

    // The version a delete closes: the replayed state, else the delete's own pre-image. A delete without a base row
    // and without a pre-image (empty Data) carries no version, so it never becomes the result.
    private static BsonDocument? VersionBeforeDelete(BsonDocument? state, HistoryEntry row) =>
        state ?? (row.Data.ElementCount > 0 ? row.Data.DeepClone().AsBsonDocument : null);
}
