using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

internal static class CheckpointStoreExtensions
{
    public static async Task<BsonDocument?> GetResumeTokenAsync(this IHistoryCheckpointStore store, CancellationToken cancellationToken) =>
        (await store.GetCheckpointAsync(cancellationToken)).ResumeToken;

    public static async Task<BsonTimestamp?> GetRestartAtAsync(this IHistoryCheckpointStore store, CancellationToken cancellationToken) =>
        (await store.GetCheckpointAsync(cancellationToken)).RestartAt;

    public static async Task<HistoryGap?> GetLastGapAsync(this IHistoryCheckpointStore store, CancellationToken cancellationToken) =>
        (await store.GetCheckpointAsync(cancellationToken)).LastGap;
}
