using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>
/// Resume token of one tracked collection, in one document of the shared checkpoint collection:
/// { _id = checkpoint id, ResumeToken, SavedAt, LastGap: { FromUtc, ToUtc } }.
/// </summary>
internal class MongoHistoryCheckpointStore : IHistoryCheckpointStore
{
    private const string ResumeTokenField = "ResumeToken";
    private const string SavedAtField = "SavedAt";
    private const string LastGapField = "LastGap";
    private const string GapFromField = "FromUtc";
    private const string GapToField = "ToUtc";

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
            new BsonDocument
            {
                { "$set", new BsonDocument(ResumeTokenField, resumeToken) },
                { "$currentDate", new BsonDocument(SavedAtField, true) },
            },
            new UpdateOptions { IsUpsert = true },
            cancellationToken);

    /// <inheritdoc />
    public async Task<HistoryGap?> GetLastGapAsync(CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(new BsonDocument("_id", _documentId))
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ReadGap(document);
    }

    /// <inheritdoc />
    public async Task<HistoryGap?> RecordGapAsync(BsonDocument? expectedToken, CancellationToken cancellationToken)
    {
        if (expectedToken is null)
            return null;

        // One atomic update: the gap starts when the lost token was saved (absent on documents written before
        // SavedAt existed) and ends at the server's now, the moment the stream restarts from now.
        var update = Builders<BsonDocument>.Update.Pipeline(new[]
        {
            new BsonDocument("$set", new BsonDocument(
                LastGapField, new BsonDocument { { GapFromField, "$" + SavedAtField }, { GapToField, "$$NOW" } })),
            new BsonDocument("$unset", ResumeTokenField),
        });
        var document = await _collection.FindOneAndUpdateAsync(
            new BsonDocument { ["_id"] = _documentId, [ResumeTokenField] = expectedToken },
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return document is null ? null : ReadGap(document);
    }

    private static HistoryGap? ReadGap(BsonDocument document)
    {
        if (!document.TryGetValue(LastGapField, out var value) || !value.IsBsonDocument)
            return null;

        var gap = value.AsBsonDocument;
        if (!gap.TryGetValue(GapToField, out var to) || !to.IsValidDateTime)
            return null;

        var from = gap.TryGetValue(GapFromField, out var fromValue) && fromValue.IsValidDateTime
            ? fromValue.ToUniversalTime()
            : (DateTime?)null;
        return new HistoryGap(from, to.ToUniversalTime());
    }
}
