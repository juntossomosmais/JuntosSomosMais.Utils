using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <summary>Idempotent setup of the audit collection: its indexes and, when configured, its expiration.</summary>
public static class MongoRequestAuditSetup
{
    internal const string CreatedAtIndexName = "CreatedAtUtc_1";

    private const int IndexOptionsConflictCode = 85;

    /// <summary>
    /// Creates the indexes on the creation date, URL, caller address and request id. With
    /// <see cref="MongoRequestAuditOptions.Retention"/> the creation date index expires the entries; changing or
    /// removing the retention later updates that index.
    /// </summary>
    public static async Task EnsureAsync(
        IMongoDatabase database, MongoRequestAuditOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var collection = database.GetCollection<BsonDocument>(options.CollectionName);
        var keys = Builders<BsonDocument>.IndexKeys;

        await collection.Indexes.CreateManyAsync(
            new[] { nameof(RequestAuditEntry.Url), nameof(RequestAuditEntry.IpAddress), nameof(RequestAuditEntry.RequestId) }
                .Select(field => new CreateIndexModel<BsonDocument>(keys.Ascending(field))),
            cancellationToken);

        var createdAt = new CreateIndexModel<BsonDocument>(
            keys.Ascending(nameof(RequestAuditEntry.CreatedAtUtc)),
            new CreateIndexOptions { Name = CreatedAtIndexName, ExpireAfter = options.Retention });
        try
        {
            await collection.Indexes.CreateOneAsync(createdAt, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == IndexOptionsConflictCode)
        {
            // The index exists with another retention.
            await ChangeRetentionAsync(database, collection, createdAt, options, cancellationToken);
        }
    }

    private static async Task ChangeRetentionAsync(
        IMongoDatabase database,
        IMongoCollection<BsonDocument> collection,
        CreateIndexModel<BsonDocument> createdAt,
        MongoRequestAuditOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Retention is { } retention)
        {
            // collMod changes the expiration in place, without rebuilding the index.
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument
                {
                    { "collMod", options.CollectionName },
                    { "index", new BsonDocument { { "name", CreatedAtIndexName }, { "expireAfterSeconds", (long)retention.TotalSeconds } } },
                },
                cancellationToken: cancellationToken);
            return;
        }

        // An expiration cannot be removed in place.
        await collection.Indexes.DropOneAsync(CreatedAtIndexName, cancellationToken);
        await collection.Indexes.CreateOneAsync(createdAt, cancellationToken: cancellationToken);
    }
}
