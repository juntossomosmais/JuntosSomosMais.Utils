using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <summary>Idempotent setup of the audit collection: its indexes.</summary>
public static class MongoRequestAuditSetup
{
    /// <summary>
    /// Creates the indexes on the creation date (used by the purge), URL, caller address and request id. Entries
    /// are never expired by the database: retention is the <see cref="IRequestAuditPurger"/>.
    /// </summary>
    public static async Task EnsureAsync(
        IMongoDatabase database, MongoRequestAuditOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var keys = Builders<BsonDocument>.IndexKeys;
        var fields = new[]
        {
            nameof(RequestAuditEntry.CreatedAtUtc),
            nameof(RequestAuditEntry.Url),
            nameof(RequestAuditEntry.IpAddress),
            nameof(RequestAuditEntry.RequestId),
        };

        await database.GetCollection<BsonDocument>(options.CollectionName).Indexes.CreateManyAsync(
            fields.Select(field => new CreateIndexModel<BsonDocument>(keys.Ascending(field))), cancellationToken);
    }
}
