using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Resume token of ONE tracked collection.</summary>
public interface IHistoryCheckpointStore
{
    /// <summary>The saved resume token, or null when there is none.</summary>
    Task<BsonDocument?> GetResumeTokenAsync(CancellationToken cancellationToken);

    /// <summary>Saves the resume token, creating the checkpoint document when it does not exist yet.</summary>
    Task SaveResumeTokenAsync(BsonDocument resumeToken, CancellationToken cancellationToken);

    /// <summary>
    /// Forgets the resume token (the next run starts from now), but only while the stored token is still
    /// <paramref name="expectedToken"/>: a stale consumer must not wipe a newer position. No-op when
    /// <paramref name="expectedToken"/> is null.
    /// </summary>
    /// <param name="expectedToken">The token this run last read or saved.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task ClearResumeTokenAsync(BsonDocument? expectedToken, CancellationToken cancellationToken);
}
