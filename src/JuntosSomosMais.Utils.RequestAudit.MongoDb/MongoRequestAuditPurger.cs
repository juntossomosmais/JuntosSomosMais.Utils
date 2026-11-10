using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <inheritdoc />
/// <param name="database">Database holding the audit collection.</param>
/// <param name="options">Names the audit collection.</param>
/// <param name="timeProvider">Clock used to pin an open upper bound; defaults to the system clock.</param>
public sealed class MongoRequestAuditPurger(
    IMongoDatabase database, MongoRequestAuditOptions options, TimeProvider? timeProvider = null) : IRequestAuditPurger
{
    /// <inheritdoc />
    public async Task<RequestAuditPurgeResult> PurgeAsync(
        RequestAuditPurgeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = ToUtc(request.FromUtc, nameof(request.FromUtc));
        var to = ToUtc(request.ToUtc, nameof(request.ToUtc));
        if (from is null && to is null)
            throw new ArgumentException("At least one of FromUtc or ToUtc is required: the purger never deletes every entry.", nameof(request));
        if (request.BatchSize <= 0)
            throw new ArgumentException("BatchSize must be positive.", nameof(request));

        var pinnedTo = to ?? (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (from >= pinnedTo)
            throw new ArgumentException(
                to is null ? "FromUtc must be before the start of the purge." : "FromUtc must be before ToUtc.", nameof(request));

        options.Validate();
        var collection = database.GetCollection<BsonDocument>(options.CollectionName);
        var filter = BuildFilter(from, pinnedTo);
        if (request.DryRun)
        {
            var matched = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return new RequestAuditPurgeResult(matched, 0, true, pinnedTo);
        }

        var deleted = await DeleteInBatchesAsync(collection, filter, request.BatchSize, cancellationToken);
        return new RequestAuditPurgeResult(deleted, deleted, false, pinnedTo);
    }

    // Deletes by batches of ids.
    private static async Task<long> DeleteInBatchesAsync(
        IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> filter, int batchSize, CancellationToken cancellationToken)
    {
        long deleted = 0;
        while (true)
        {
            var ids = await collection
                .Find(filter)
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .Limit(batchSize)
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
                return deleted;

            var result = await collection.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("_id", ids.Select(d => d["_id"])), cancellationToken);
            deleted += result.DeletedCount;
        }
    }

    private static FilterDefinition<BsonDocument> BuildFilter(DateTime? fromUtc, DateTime toUtc)
    {
        var builder = Builders<BsonDocument>.Filter;
        var upper = builder.Lt(nameof(RequestAuditEntry.CreatedAtUtc), toUtc);
        return fromUtc.HasValue
            ? builder.And(builder.Gte(nameof(RequestAuditEntry.CreatedAtUtc), fromUtc.Value), upper)
            : upper;
    }

    private static DateTime? ToUtc(DateTime? value, string name) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => throw new ArgumentException($"{name} must have DateTimeKind Utc or Local, not Unspecified.", name),
    };
}
