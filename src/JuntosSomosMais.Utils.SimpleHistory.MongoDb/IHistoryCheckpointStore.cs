using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Resume token of ONE tracked collection.</summary>
internal interface IHistoryCheckpointStore
{
    /// <summary>The saved position, or <see cref="HistoryCheckpoint.Empty"/> when nothing was saved.</summary>
    Task<HistoryCheckpoint> GetCheckpointAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the resume token and when it was saved, creating the checkpoint document when it does not exist yet. Clears
    /// the restart time of a previous gap.
    /// </summary>
    Task SaveResumeTokenAsync(BsonDocument resumeToken, CancellationToken cancellationToken);

    /// <summary>
    /// Records a gap, forgets the resume token and saves the cluster time the gap ends at (the next run starts there),
    /// but only while the stored position is still the expected one: a stale consumer must not wipe a newer position.
    /// The gap runs from the time the last token was saved to now. Returns it, or null when nothing was recorded (no
    /// expected position, or another consumer saved a newer one).
    /// </summary>
    /// <param name="expectedToken">The token this run last read or saved.</param>
    /// <param name="expectedRestartAt">The restart time this run started from, when it had no token.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<HistoryGap?> RecordGapAsync(
        BsonDocument? expectedToken, BsonTimestamp? expectedRestartAt, CancellationToken cancellationToken);
}
