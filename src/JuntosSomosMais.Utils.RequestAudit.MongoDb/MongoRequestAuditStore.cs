using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <summary>Stores each entry as one document of the configured collection.</summary>
public sealed class MongoRequestAuditStore(IMongoDatabase database, MongoRequestAuditOptions options) : IRequestAuditStore
{
    /// <inheritdoc />
    public Task SaveAsync(RequestAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return database
            .GetCollection<BsonDocument>(options.CollectionName)
            .InsertOneAsync(ToDocument(entry), cancellationToken: cancellationToken);
    }

    internal static BsonDocument ToDocument(RequestAuditEntry entry) => new()
    {
        // As text: a binary Guid is stored differently depending on the Guid representation the app configured.
        { "_id", entry.Id.ToString() },
        { nameof(RequestAuditEntry.CreatedAtUtc), entry.CreatedAtUtc },
        { nameof(RequestAuditEntry.Method), entry.Method },
        { nameof(RequestAuditEntry.Url), entry.Url },
        { nameof(RequestAuditEntry.QueryParams), ToArray(entry.QueryParams) },
        { nameof(RequestAuditEntry.IpAddress), ToValue(entry.IpAddress) },
        { nameof(RequestAuditEntry.RequestHeaders), ToArray(entry.RequestHeaders) },
        { nameof(RequestAuditEntry.RequestBody), ToValue(entry.RequestBody) },
        { nameof(RequestAuditEntry.RequestBodyNote), ToValue(entry.RequestBodyNote) },
        { nameof(RequestAuditEntry.ResponseStatusCode), entry.ResponseStatusCode },
        { nameof(RequestAuditEntry.ResponseHeaders), ToArray(entry.ResponseHeaders) },
        { nameof(RequestAuditEntry.ResponseBody), ToValue(entry.ResponseBody) },
        { nameof(RequestAuditEntry.ResponseBodyNote), ToValue(entry.ResponseBodyNote) },
        { nameof(RequestAuditEntry.RequestId), entry.RequestId },
        { nameof(RequestAuditEntry.TraceParent), ToValue(entry.TraceParent) },
        { nameof(RequestAuditEntry.Caller), ToArray(entry.Caller) },
    };

    // Name/Value pairs instead of a sub-document: a header, a query parameter or a claim type may contain "." or
    // start with "$", which are not safe as field names.
    private static BsonArray ToArray(IReadOnlyDictionary<string, string> values) =>
        new(values.Select(pair => new BsonDocument { { "Name", pair.Key }, { "Value", pair.Value } }));

    private static BsonValue ToValue(string? value) => value is null ? BsonNull.Value : new BsonString(value);
}
