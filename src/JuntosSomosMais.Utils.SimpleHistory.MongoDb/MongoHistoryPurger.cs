using JuntosSomosMais.Utils.Shared;
using MongoDB.Bson;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <inheritdoc />
/// <param name="database">Database holding the history collections.</param>
/// <param name="timeProvider">Clock used to pin an open upper bound; defaults to the system clock.</param>
public sealed class MongoHistoryPurger(IMongoDatabase database, TimeProvider? timeProvider = null) : IHistoryPurger
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryPurgeResult>> PurgeAsync(
        IEnumerable<HistoryOptions> sources, HistoryPurgeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(request);

        var (from, pinnedTo) = PurgeInterval.Resolve(
            request.FromUtc, request.ToUtc, request.BatchSize, timeProvider, "row");

        var list = sources.ToList();
        foreach (var source in list)
            source.Validate();

        var filter = BuildFilter(from, pinnedTo);
        var results = new List<HistoryPurgeResult>(list.Count);
        foreach (var historyName in list.Select(source => source.HistoryName))
        {
            var collection = database.GetCollection<BsonDocument>(historyName);
            if (request.DryRun)
            {
                var matched = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
                results.Add(new HistoryPurgeResult(historyName, matched, 0, true, pinnedTo));
            }
            else
            {
                var deleted = await DeleteInBatchesAsync(collection, filter, request.BatchSize, ct);
                results.Add(new HistoryPurgeResult(historyName, deleted, deleted, false, pinnedTo));
            }
        }

        return results;
    }

    // Selects the ids of one batch through the HistoryDate index, then deletes exactly those rows: each round trip
    // is bounded, so a large purge never holds one long-running delete on the collection or the oplog.
    private static async Task<long> DeleteInBatchesAsync(
        IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, int batchSize, CancellationToken ct)
    {
        long deleted = 0;
        while (true)
        {
            var ids = await collection
                .Find(filter)
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .Limit(batchSize)
                .ToListAsync(ct);
            if (ids.Count == 0)
                return deleted;

            var result = await collection.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("_id", ids.Select(d => d["_id"])), ct);
            deleted += result.DeletedCount;
        }
    }

    /// <summary><c>HistoryDate &gt;= from</c> (when set) and <c>HistoryDate &lt; to</c> (when set).</summary>
    internal static FilterDefinition<BsonDocument> BuildFilter(DateTime? fromUtc, DateTime? toUtc)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>(2);
        if (fromUtc.HasValue)
            filters.Add(builder.Gte(nameof(HistoryEntry.HistoryDate), fromUtc.Value));
        if (toUtc.HasValue)
            filters.Add(builder.Lt(nameof(HistoryEntry.HistoryDate), toUtc.Value));
        return builder.And(filters);
    }
}
