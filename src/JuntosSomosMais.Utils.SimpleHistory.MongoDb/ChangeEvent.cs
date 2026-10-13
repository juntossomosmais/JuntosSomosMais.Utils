using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Driver-independent view of a change stream event. Decouples mapping from driver types.</summary>
internal sealed record ChangeEvent(
    ChangeStreamOperationType Operation,
    BsonValue EntityId,
    BsonDocument? Before,
    BsonDocument? After,
    BsonDocument? UpdatedFields,
    IReadOnlyList<string>? RemovedFieldNames,
    string Token,
    DateTime TimestampUtc,
    IReadOnlyList<string>? TruncatedArrayFieldNames = null);
