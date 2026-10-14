using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>The indexes of a history collection, named after it so several sources never collide.</summary>
internal static class HistoryIndexes
{
    /// <summary>The three indexes of the history collection <paramref name="historyName"/>.</summary>
    public static CreateIndexModel<HistoryEntry>[] For(string historyName)
    {
        var keys = Builders<HistoryEntry>.IndexKeys;
        return
        [
            // One entity's history, newest first, in oplog order.
            new(
                keys.Ascending(e => e.EntityId).Descending(e => e.ChangeToken),
                new CreateIndexOptions { Name = historyName + "-entity-id-change-token" }),
            // Makes inserts idempotent on redelivery.
            new(
                keys.Ascending(e => e.ChangeToken),
                new CreateIndexOptions { Name = historyName + "-change-token", Unique = true }),
            // Lets a retention purge delete by date range without scanning the whole collection.
            new(
                keys.Ascending(e => e.HistoryDate),
                new CreateIndexOptions { Name = historyName + "-history-date" }),
        ];
    }
}
