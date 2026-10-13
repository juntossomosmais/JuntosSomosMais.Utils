using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Resume token of ONE tracked collection.</summary>
internal interface IHistoryCheckpointStore
{
    /// <summary>The saved resume token, or null when there is none.</summary>
    Task<BsonDocument?> GetResumeTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the resume token and when it was saved, creating the checkpoint document when it does not exist yet.
    /// </summary>
    Task SaveResumeTokenAsync(BsonDocument resumeToken, CancellationToken cancellationToken);

    /// <summary>The last recorded gap, or null when there is none.</summary>
    Task<HistoryGap?> GetLastGapAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records a gap and forgets the resume token (the next run starts from now), but only while the stored token is
    /// still <paramref name="expectedToken"/>: a stale consumer must not wipe a newer position. The gap runs from the
    /// time that token was saved to now. Returns it, or null when nothing was recorded (no expected token, or another
    /// consumer saved a newer position).
    /// </summary>
    /// <param name="expectedToken">The token this run last read or saved.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<HistoryGap?> RecordGapAsync(BsonDocument? expectedToken, CancellationToken cancellationToken);
}
