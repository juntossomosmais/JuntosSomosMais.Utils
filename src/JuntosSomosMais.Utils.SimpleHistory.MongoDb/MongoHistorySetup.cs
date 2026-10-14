using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>
/// Idempotent setup of ONE tracked collection: pre/post-images on the source, the (optionally compressed) history
/// collection, the shared checkpoint collection and the history indexes.
/// </summary>
public class MongoHistorySetup(IMongoDatabase database, MongoHistoryOptions options) : IHistorySetup
{
    private const int NamespaceExistsCode = 48;

    private static readonly BsonDocument ZstdStorageEngine =
        new("wiredTiger", new BsonDocument("configString", "block_compressor=zstd"));

    /// <summary>Runs the setup of every source (for a console / index tool that does not host the streams).</summary>
    public static async Task EnsureAsync(
        IMongoDatabase database, IEnumerable<MongoHistoryOptions> options, CancellationToken cancellationToken = default)
    {
        var list = options.ToList();
        foreach (var item in list)
            item.Validate();

        foreach (var item in list)
            await new MongoHistorySetup(database, item).EnsureSetupAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnsureSetupAsync(CancellationToken cancellationToken = default)
    {
        var existing = await ListExistingCollectionsAsync(cancellationToken);

        await EnsureSourceAsync(existing, cancellationToken);

        // The history collection (zstd when enabled) BEFORE its indexes: creating an index on a missing collection
        // would create it uncompressed.
        await EnsureCollectionAsync(
            options.HistoryName,
            existing,
            options.CompressHistory ? new CreateCollectionOptions { StorageEngine = ZstdStorageEngine } : null,
            cancellationToken);
        await EnsureCollectionAsync(options.CheckpointName, existing, null, cancellationToken);

        await database.GetCollection<HistoryEntry>(options.HistoryName).Indexes
            .CreateManyAsync(HistoryIndexes.For(options.HistoryName), cancellationToken);
    }

    // One round trip for the three collections. The entries carry the collection options, which tell whether
    // pre/post-images are already enabled on the source.
    private async Task<Dictionary<string, BsonDocument>> ListExistingCollectionsAsync(CancellationToken cancellationToken)
    {
        var names = new BsonArray { options.SourceName, options.HistoryName, options.CheckpointName };
        using var cursor = await database.ListCollectionsAsync(
            new ListCollectionsOptions { Filter = new BsonDocument("name", new BsonDocument("$in", names)) },
            cancellationToken);
        return (await cursor.ToListAsync(cancellationToken)).ToDictionary(c => c["name"].AsString);
    }

    // collMod takes an exclusive lock on the (hot) source collection, so it only runs when images are not enabled yet.
    private async Task EnsureSourceAsync(Dictionary<string, BsonDocument> existing, CancellationToken cancellationToken)
    {
        if (existing.TryGetValue(options.SourceName, out var source) && HasPreAndPostImages(source))
            return;

        // The source must exist before collMod.
        await EnsureCollectionAsync(options.SourceName, existing, null, cancellationToken);
        await database.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                { "collMod", options.SourceName },
                { "changeStreamPreAndPostImages", new BsonDocument("enabled", true) },
            },
            cancellationToken: cancellationToken);
    }

    private static bool HasPreAndPostImages(BsonDocument collectionInfo) =>
        collectionInfo.TryGetValue("options", out var collectionOptions)
        && collectionOptions.IsBsonDocument
        && collectionOptions.AsBsonDocument.TryGetValue("changeStreamPreAndPostImages", out var images)
        && images.IsBsonDocument
        && images.AsBsonDocument.GetValue("enabled", false) is BsonBoolean { Value: true };

    private async Task EnsureCollectionAsync(
        string name,
        Dictionary<string, BsonDocument> existing,
        CreateCollectionOptions? createOptions,
        CancellationToken cancellationToken)
    {
        if (existing.ContainsKey(name))
            return;

        try
        {
            await database.CreateCollectionAsync(name, createOptions, cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == NamespaceExistsCode)
        {
            // Created concurrently by another replica between the listing and the create: it already exists, fine.
        }
    }
}
