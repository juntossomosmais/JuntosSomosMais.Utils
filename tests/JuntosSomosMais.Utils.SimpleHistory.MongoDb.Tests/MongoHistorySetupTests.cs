using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistorySetupTests : MongoTestBase
{
    private static MongoHistoryOptions Orders => new("orders");

    private async Task<BsonDocument?> GetCollectionInfoAsync(string name)
    {
        using var cursor = await Database.ListCollectionsAsync(
            new ListCollectionsOptions { Filter = new BsonDocument("name", name) });
        return (await cursor.ToListAsync()).SingleOrDefault();
    }

    private async Task<List<BsonDocument>> GetIndexesAsync(string collection)
    {
        using var cursor = await Database.GetCollection<BsonDocument>(collection).Indexes.ListAsync();
        return await cursor.ToListAsync();
    }

    [Fact]
    public async Task EnsureSetupAsync_ShouldEnablePreAndPostImagesOnSource()
    {
        await new MongoHistorySetup(Database, Orders).EnsureSetupAsync(CancellationToken.None);

        var info = await GetCollectionInfoAsync("orders");

        Assert.NotNull(info);
        Assert.True(info!["options"]["changeStreamPreAndPostImages"]["enabled"].AsBoolean);
    }

    [Fact]
    public async Task EnsureSetupAsync_ShouldCreateHistoryCollectionWithZstdCompression()
    {
        await new MongoHistorySetup(Database, Orders).EnsureSetupAsync(CancellationToken.None);

        var info = await GetCollectionInfoAsync("ordersHistory");

        Assert.NotNull(info);
        Assert.Contains("block_compressor=zstd", info!["options"]["storageEngine"]["wiredTiger"]["configString"].AsString);
    }

    [Fact]
    public async Task EnsureSetupAsync_WhenCompressionIsDisabled_ShouldCreateHistoryCollectionWithoutStorageOptions()
    {
        var options = new MongoHistoryOptions("orders") { CompressHistory = false };

        await new MongoHistorySetup(Database, options).EnsureSetupAsync(CancellationToken.None);

        var info = await GetCollectionInfoAsync("ordersHistory");
        Assert.NotNull(info);
        Assert.False(info!["options"].AsBsonDocument.Contains("storageEngine"));
    }

    [Fact]
    public async Task EnsureSetupAsync_WhenHistoryCollectionIsOverridden_ShouldUseIt()
    {
        var options = new MongoHistoryOptions("orders") { HistoryName = "orders_audit" };

        await new MongoHistorySetup(Database, options).EnsureSetupAsync(CancellationToken.None);

        Assert.NotNull(await GetCollectionInfoAsync("orders_audit"));
        Assert.Null(await GetCollectionInfoAsync("ordersHistory"));
        Assert.Contains(await GetIndexesAsync("orders_audit"), i => i["name"].AsString == "orders_audit-change-token");
    }

    [Fact]
    public async Task EnsureSetupAsync_ShouldCreateCheckpointCollection()
    {
        await new MongoHistorySetup(Database, Orders).EnsureSetupAsync(CancellationToken.None);

        Assert.NotNull(await GetCollectionInfoAsync("simpleHistoryCheckpoint"));
    }

    [Fact]
    public async Task EnsureSetupAsync_ShouldCreateDerivedIndexesBuiltFromBsonFieldNames()
    {
        await new MongoHistorySetup(Database, Orders).EnsureSetupAsync(CancellationToken.None);

        var indexes = await GetIndexesAsync("ordersHistory");

        var token = Assert.Single(indexes, i => i["name"].AsString == "ordersHistory-change-token");
        Assert.True(token.GetValue("unique", false).AsBoolean);
        Assert.Equal(new BsonDocument("ChangeToken", 1), token["key"].AsBsonDocument);
        var entity = Assert.Single(indexes, i => i["name"].AsString == "ordersHistory-entity-id-history-date");
        Assert.Equal(new BsonDocument { { "EntityId", 1 }, { "HistoryDate", -1 }, { "ChangeToken", -1 } }, entity["key"].AsBsonDocument);
        Assert.False(entity.GetValue("unique", false).AsBoolean);
        var date = Assert.Single(indexes, i => i["name"].AsString == "ordersHistory-history-date");
        Assert.Equal(new BsonDocument("HistoryDate", 1), date["key"].AsBsonDocument);
        Assert.False(date.GetValue("unique", false).AsBoolean);
    }

    [Fact]
    public async Task EnsureSetupAsync_WhenCalledTwice_ShouldBeIdempotent()
    {
        var sut = new MongoHistorySetup(Database, Orders);
        await sut.EnsureSetupAsync(CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => sut.EnsureSetupAsync(CancellationToken.None));

        Assert.Null(exception);
        var info = await GetCollectionInfoAsync("ordersHistory");
        Assert.Contains("block_compressor=zstd", info!["options"]["storageEngine"]["wiredTiger"]["configString"].AsString);
        Assert.Equal(4, (await GetIndexesAsync("ordersHistory")).Count); // _id_ + the three derived ones
    }

    [Fact]
    public async Task EnsureSetupAsync_WhenDatabaseWasDropped_ShouldRecreateEverything()
    {
        var sut = new MongoHistorySetup(Database, Orders);
        await sut.EnsureSetupAsync(CancellationToken.None);
        await Client.DropDatabaseAsync(DatabaseName);

        await sut.EnsureSetupAsync(CancellationToken.None);

        var source = await GetCollectionInfoAsync("orders");
        Assert.True(source!["options"]["changeStreamPreAndPostImages"]["enabled"].AsBoolean);
        Assert.NotNull(await GetCollectionInfoAsync("ordersHistory"));
    }

    [Fact]
    public async Task EnsureSetupAsync_WhenSourceAlreadyExists_ShouldKeepItsDocuments()
    {
        await Database.GetCollection<BsonDocument>("orders").InsertOneAsync(new BsonDocument("A", 1));

        await new MongoHistorySetup(Database, Orders).EnsureSetupAsync(CancellationToken.None);

        Assert.Equal(1, await Database.GetCollection<BsonDocument>("orders").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task EnsureAsync_WithTwoSources_ShouldSetUpEachWithItsOwnCollectionsAndShareOneCheckpointCollection()
    {
        var sources = new[] { new MongoHistoryOptions("orders"), new MongoHistoryOptions("carts") { CompressHistory = false } };

        await MongoHistorySetup.EnsureAsync(Database, sources, CancellationToken.None);

        foreach (var name in new[] { "orders", "carts" })
        {
            var info = await GetCollectionInfoAsync(name);
            Assert.True(info!["options"]["changeStreamPreAndPostImages"]["enabled"].AsBoolean);
            Assert.NotNull(await GetCollectionInfoAsync(name + "History"));
            Assert.Contains(await GetIndexesAsync(name + "History"), i => i["name"].AsString == name + "History-change-token");
        }

        var orders = await GetCollectionInfoAsync("ordersHistory");
        Assert.Contains("zstd", orders!["options"]["storageEngine"]["wiredTiger"]["configString"].AsString);
        var carts = await GetCollectionInfoAsync("cartsHistory");
        Assert.False(carts!["options"].AsBsonDocument.Contains("storageEngine"));
        using var all = await Database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = new BsonDocument("name", "simpleHistoryCheckpoint") });
        Assert.Single(await all.ToListAsync());
    }

    [Fact]
    public async Task EnsureAsync_ShouldRejectInvalidOptionsBeforeTouchingTheDatabase()
    {
        var invalid = new MongoHistoryOptions("orders") { BatchSize = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MongoHistorySetup.EnsureAsync(Database, new[] { invalid }, CancellationToken.None));

        using var names = await Database.ListCollectionNamesAsync();
        Assert.Empty(await names.ToListAsync());
    }
}
