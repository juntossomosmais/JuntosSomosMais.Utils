using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>
/// Resume token of one tracked collection, in one document of the shared checkpoint collection:
/// { _id = checkpoint id, ResumeToken }.
/// </summary>
public class MongoHistoryCheckpointStore : IHistoryCheckpointStore
{
    private const string ResumeTokenField = "ResumeToken";

    private readonly IMongoCollection<BsonDocument> _collection;

    private readonly string _documentId;

    /// <param name="database">Database holding the checkpoint collection.</param>
    /// <param name="checkpointCollection">Shared collection with one document per tracked collection.</param>
    /// <param name="checkpointId">Id of this source's document.</param>
    public MongoHistoryCheckpointStore(IMongoDatabase database, string checkpointCollection, string checkpointId)
    {
        _collection = database.GetCollection<BsonDocument>(checkpointCollection);
        _documentId = checkpointId;
    }

    /// <inheritdoc />
    public async Task<BsonDocument?> GetResumeTokenAsync(CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(new BsonDocument("_id", _documentId))
            .FirstOrDefaultAsync(cancellationToken);
        return document is not null && document.TryGetValue(ResumeTokenField, out var token) && token.IsBsonDocument
            ? token.AsBsonDocument
            : null;
    }

    /// <inheritdoc />
    public Task SaveResumeTokenAsync(BsonDocument resumeToken, CancellationToken cancellationToken) =>
        _collection.UpdateOneAsync(
            new BsonDocument("_id", _documentId),
            new BsonDocument("$set", new BsonDocument(ResumeTokenField, resumeToken)),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

    /// <inheritdoc />
    public Task ClearResumeTokenAsync(BsonDocument? expectedToken, CancellationToken cancellationToken) =>
        expectedToken is null
            ? Task.CompletedTask
            : _collection.UpdateOneAsync(
                new BsonDocument { ["_id"] = _documentId, [ResumeTokenField] = expectedToken },
                new BsonDocument("$unset", new BsonDocument(ResumeTokenField, "")),
                cancellationToken: cancellationToken);
}
