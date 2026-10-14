using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>The saved position of one tracked collection, read in one round trip.</summary>
/// <param name="ResumeToken">The saved resume token, or null when there is none.</param>
/// <param name="RestartAt">The cluster time the last gap ended at, or null once a token was saved after it.</param>
/// <param name="LastGap">The last recorded gap, or null when there is none.</param>
internal sealed record HistoryCheckpoint(BsonDocument? ResumeToken, BsonTimestamp? RestartAt, HistoryGap? LastGap)
{
    public static readonly HistoryCheckpoint Empty = new(null, null, null);
}
