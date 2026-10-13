using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryPurgerTests : MongoTestBase
{
    private static readonly DateTime Cutoff = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static MongoHistoryOptions Orders => new("orders");

    private MongoHistoryPurger Sut => new(Database);

    private IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    private Task SeedAsync(string collection, params DateTime[] dates) =>
        Collection(collection).InsertManyAsync(dates.Select((date, i) => new BsonDocument
        {
            { "EntityId", "e" + i },
            { "HistoryDate", date },
            { "HistoryType", HistoryTypes.Created },
            { "Data", new BsonDocument("N", i) },
            { "ChangeToken", collection + "-" + Guid.NewGuid().ToString("N") },
        }));

    private async Task<List<DateTime>> DatesAsync(string collection) =>
        (await Collection(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
        .Select(d => d["HistoryDate"].ToUniversalTime()).OrderBy(d => d).ToList();

    [Fact]
    public async Task PurgeAsync_WithToOnly_ShouldDeleteRowsBeforeItAndKeepTheRowExactlyAtIt()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-2), Cutoff.AddSeconds(-1), Cutoff, Cutoff.AddDays(1));

        var results = await Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, Cutoff, false));

        var result = Assert.Single(results);
        Assert.Equal(new HistoryPurgeResult("ordersHistory", 2, 2, false, Cutoff), result);
        Assert.Equal(new[] { Cutoff, Cutoff.AddDays(1) }, await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithFromOnly_ShouldDeleteTheRowExactlyAtItAndLaterOnesAndKeepEarlierRows()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-2), Cutoff.AddSeconds(-1), Cutoff, Cutoff.AddDays(1));

        // "Now" is far after every seeded row, so the pinned bound keeps the from-only semantics here.
        var sut = new MongoHistoryPurger(Database, new FakeTimeProvider(Cutoff.AddYears(1)));

        var results = await sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(Cutoff, null, false));

        Assert.Equal(2, Assert.Single(results).Deleted);
        Assert.Equal(Cutoff.AddYears(1), results[0].ToUtc);
        Assert.Equal(new[] { Cutoff.AddDays(-2), Cutoff.AddSeconds(-1) }, await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithFromOnly_ShouldPinTheUpperBoundToTheStartAndKeepLaterRows()
    {
        // "Now" is Cutoff: a row dated after it stands for one the worker writes while the purge runs.
        var sut = new MongoHistoryPurger(Database, new FakeTimeProvider(Cutoff));
        await SeedAsync("ordersHistory", Cutoff.AddDays(-2), Cutoff.AddSeconds(-1), Cutoff, Cutoff.AddHours(1));

        var result = Assert.Single(await sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(Cutoff.AddDays(-10), null, false)));

        Assert.Equal(new HistoryPurgeResult("ordersHistory", 2, 2, false, Cutoff), result);
        Assert.Equal(new[] { Cutoff, Cutoff.AddHours(1) }, await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithBothBounds_ShouldDeleteOnlyTheHalfOpenInterval()
    {
        var from = Cutoff;
        var to = Cutoff.AddDays(10);
        await SeedAsync("ordersHistory", from.AddSeconds(-1), from, from.AddDays(5), to.AddSeconds(-1), to, to.AddDays(1));

        var results = await Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(from, to, false));

        Assert.Equal(3, Assert.Single(results).Deleted);
        Assert.Equal(new[] { from.AddSeconds(-1), to, to.AddDays(1) }, await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_OnDryRun_ShouldDeleteNothingAndMatchWhatARealRunDeletes()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-3), Cutoff.AddDays(-2), Cutoff, Cutoff.AddDays(1));
        var request = new HistoryPurgeRequest(null, Cutoff, true);

        var dry = Assert.Single(await Sut.PurgeAsync(new[] { Orders }, request));

        Assert.Equal(new HistoryPurgeResult("ordersHistory", 2, 0, true, Cutoff), dry);
        Assert.Equal(4, (await DatesAsync("ordersHistory")).Count);

        var real = Assert.Single(await Sut.PurgeAsync(new[] { Orders }, request with { DryRun = false }));

        Assert.Equal(dry.Matched, real.Deleted);
        Assert.Equal(dry.Matched, real.Matched);
        Assert.False(real.DryRun);
    }

    [Fact]
    public async Task PurgeAsync_WithSeveralSources_ShouldPurgeEachInInputOrderAndNotCreateMissingCollections()
    {
        var carts = new HistoryOptions("carts") { HistoryName = "cartsAudit" };
        var invoices = new HistoryOptions("invoices");
        await SeedAsync("ordersHistory", Cutoff.AddDays(-1), Cutoff.AddDays(1));
        await SeedAsync("cartsAudit", Cutoff.AddDays(-5), Cutoff.AddDays(-4), Cutoff.AddDays(-3));

        var results = await Sut.PurgeAsync(new[] { Orders, invoices, carts }, new HistoryPurgeRequest(null, Cutoff, false));

        Assert.Equal(
            new[]
            {
                new HistoryPurgeResult("ordersHistory", 1, 1, false, Cutoff),
                new HistoryPurgeResult("invoicesHistory", 0, 0, false, Cutoff),
                new HistoryPurgeResult("cartsAudit", 3, 3, false, Cutoff),
            },
            results);
        Assert.Single(await DatesAsync("ordersHistory"));
        Assert.Empty(await DatesAsync("cartsAudit"));
        using var names = await Database.ListCollectionNamesAsync();
        Assert.DoesNotContain("invoicesHistory", await names.ToListAsync());
    }

    [Fact]
    public async Task PurgeAsync_ShouldNeverTouchTheSourceOrTheCheckpointCollection()
    {
        var old = Cutoff.AddYears(-3);
        await Collection("orders").InsertManyAsync(new[]
        {
            new BsonDocument { { "_id", 1 }, { "HistoryDate", old } },
            new BsonDocument { { "_id", 2 }, { "HistoryDate", Cutoff.AddDays(1) } },
        });
        await Collection("simpleHistoryCheckpoint").InsertOneAsync(
            new BsonDocument { { "_id", "orders" }, { "HistoryDate", old }, { "ResumeToken", "abc" } });
        await SeedAsync("ordersHistory", old, Cutoff.AddDays(1));
        var sourceBefore = await Collection("orders").Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync();
        var checkpointBefore = await Collection("simpleHistoryCheckpoint").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();

        await Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, Cutoff, false));

        Assert.Equal(sourceBefore, await Collection("orders").Find(FilterDefinition<BsonDocument>.Empty).SortBy(d => d["_id"]).ToListAsync());
        Assert.Equal(checkpointBefore, await Collection("simpleHistoryCheckpoint").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.Single(await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithBatchSizeSmallerThanMatches_ShouldDeleteEverythingInSeveralRounds()
    {
        // Arrange
        await SeedAsync("ordersHistory",
            Cutoff.AddDays(-5), Cutoff.AddDays(-4), Cutoff.AddDays(-3), Cutoff.AddDays(-2), Cutoff.AddDays(-1),
            Cutoff, Cutoff.AddDays(1));
        var deleteCommands = 0;
        var sut = new MongoHistoryPurger(ObservedDatabase(e =>
        {
            if (e.CommandName == "delete" && e.Command["delete"] == "ordersHistory")
                Interlocked.Increment(ref deleteCommands);
        }));

        // Act
        var result = Assert.Single(await sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, Cutoff, false, BatchSize: 2)));

        // Assert: 5 matches in batches of 2 take three rounds (2, 2, 1).
        Assert.Equal(new HistoryPurgeResult("ordersHistory", 5, 5, false, Cutoff), result);
        Assert.Equal(new[] { Cutoff, Cutoff.AddDays(1) }, await DatesAsync("ordersHistory"));
        Assert.Equal(3, Volatile.Read(ref deleteCommands));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PurgeAsync_WithNonPositiveBatchSize_ShouldThrowAndDeleteNothing(int batchSize)
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, Cutoff, false, batchSize)));

        Assert.Contains("BatchSize", ex.Message);
        Assert.Single(await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithNoBound_ShouldThrowAndDeleteNothing()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, null, false)));

        Assert.Single(await DatesAsync("ordersHistory"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PurgeAsync_WhenFromIsNotBeforeTo_ShouldThrow(int daysFromIsAfterTo)
    {
        var to = Cutoff;
        var from = to.AddDays(daysFromIsAfterTo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(from, to, false)));
    }

    [Fact]
    public async Task PurgeAsync_WithFromOnlyAfterTheStartOfThePurge_ShouldThrowAndDeleteNothing()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(1));
        var sut = new MongoHistoryPurger(Database, new FakeTimeProvider(Cutoff));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(Cutoff.AddHours(1), null, false)));

        Assert.Contains("start of the purge", ex.Message);
        Assert.Single(await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithUnspecifiedKind_ShouldThrow()
    {
        var unspecified = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<ArgumentException>(
            () => Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, unspecified, false)));
    }

    [Fact]
    public async Task PurgeAsync_WithLocalKind_ShouldBeConvertedToUtc()
    {
        await SeedAsync("ordersHistory", Cutoff.AddHours(-1), Cutoff, Cutoff.AddHours(1));
        var localCutoff = Cutoff.ToLocalTime();
        Assert.Equal(DateTimeKind.Local, localCutoff.Kind);

        var result = Assert.Single(await Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, localCutoff, false)));

        Assert.Equal(1, result.Deleted);
        Assert.Equal(new[] { Cutoff, Cutoff.AddHours(1) }, await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_WithInvalidOptions_ShouldThrowBeforeDeletingAnything()
    {
        await SeedAsync("ordersHistory", Cutoff.AddDays(-1));
        var invalid = new HistoryOptions("carts") { BatchSize = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Sut.PurgeAsync(new[] { Orders, invalid }, new HistoryPurgeRequest(null, Cutoff, false)));

        Assert.Single(await DatesAsync("ordersHistory"));
    }

    [Fact]
    public async Task PurgeAsync_AfterDeletingAnEntitysBaseRow_ShouldMakeItsReplayThrowWhileOtherEntitiesStillRebuild()
    {
        var old = Cutoff.AddDays(-10);
        var recent = Cutoff.AddDays(1);
        await Collection("ordersHistory").InsertManyAsync(new[]
        {
            Row("lost", old, HistoryTypes.Created, new BsonDocument("Status", "A")),
            Row("lost", recent, HistoryTypes.Changed, new BsonDocument("Status", "B")),
            Row("kept", recent.AddHours(-1), HistoryTypes.Created, new BsonDocument("Status", "A")),
            Row("kept", recent, HistoryTypes.Changed, new BsonDocument("Status", "B")),
        });

        await Sut.PurgeAsync(new[] { Orders }, new HistoryPurgeRequest(null, Cutoff, false));

        Assert.Throws<InvalidOperationException>(() => HistoryReplay.Rebuild(RowsOf("lost")));
        Assert.Equal("B", HistoryReplay.Rebuild(RowsOf("kept"))["Status"].AsString);

        static BsonDocument Row(string entity, DateTime date, string type, BsonDocument data) => new()
        {
            { "EntityId", entity }, { "HistoryDate", date }, { "HistoryType", type }, { "Data", data },
            { "ChangeToken", Guid.NewGuid().ToString("N") },
        };

        IEnumerable<HistoryEntry> RowsOf(string entity) =>
            Collection("ordersHistory").Find(new BsonDocument("EntityId", entity)).SortBy(d => d["HistoryDate"]).ToList()
                .Select(d => BsonSerializer.Deserialize<HistoryEntry>(d));
    }

    [Fact]
    public async Task BuildFilter_ShouldBeServedByTheHistoryDateIndex()
    {
        await MongoHistorySetup.EnsureAsync(Database, new[] { Orders });
        await SeedAsync("ordersHistory", Enumerable.Range(0, 50).Select(i => Cutoff.AddDays(i - 25)).ToArray());
        var filter = MongoHistoryPurger.BuildFilter(null, Cutoff)
            .Render(new RenderArgs<BsonDocument>(BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>(), BsonSerializer.SerializerRegistry));

        // The batch selection the purger runs: filter, _id projection, limit.
        var explain = await Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "explain", new BsonDocument { { "find", "ordersHistory" }, { "filter", filter }, { "projection", new BsonDocument("_id", 1) }, { "limit", 5000 } } },
            { "verbosity", "queryPlanner" },
        });

        var plan = explain.ToJson();
        Assert.Contains("IXSCAN", plan);
        Assert.Contains("ordersHistory-history-date", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
    }
}
