using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryProcessorIntegrationTests : MongoTestBase
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);

    private readonly MongoHistoryCheckpointStore _checkpoints;
    private readonly List<(CancellationTokenSource Cts, Task Task)> _running = new();

    public MongoHistoryProcessorIntegrationTests()
    {
        _checkpoints = new MongoHistoryCheckpointStore(Database, OrdersOptions.CheckpointName, OrdersOptions.CheckpointId);
        new MongoHistorySetup(Database, OrdersOptions).EnsureSetupAsync().GetAwaiter().GetResult();
        SeedResumeTokenAsync().GetAwaiter().GetResult();
    }

    private MongoHistoryOptions OrdersOptions { get; } = new MongoHistoryOptions("orders").IgnoreFields("CreationDate");

    private IMongoCollection<BsonDocument> Orders => Database.GetCollection<BsonDocument>("orders");
    private IMongoCollection<HistoryEntry> History => Database.GetCollection<HistoryEntry>("ordersHistory");
    private IMongoCollection<BsonDocument> Checkpoints => Database.GetCollection<BsonDocument>("simpleHistoryCheckpoint");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var (cts, task) in _running)
            {
                cts.Cancel();
                try
                { task.Wait(TimeSpan.FromSeconds(10)); }
                catch (Exception) { /* cancelled */ }
                cts.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Stores the current stream position so the processor starts exactly here (a stream opened on "now" would
    /// race with the test's first write).
    /// </summary>
    private Task SeedResumeTokenAsync() => SeedResumeTokenAsync(Orders, _checkpoints);

    private static async Task SeedResumeTokenAsync(IMongoCollection<BsonDocument> source, MongoHistoryCheckpointStore store)
    {
        using var cursor = await source.WatchAsync(new EmptyPipelineDefinition<ChangeStreamDocument<BsonDocument>>());
        await store.SaveResumeTokenAsync(cursor.GetResumeToken(), CancellationToken.None);
    }

    private MongoHistoryProcessor CreateProcessor() =>
        new(Database, OrdersOptions, _checkpoints, new HistoryHealthState(), NullLogger.Instance);

    private (CancellationTokenSource Cts, Task Task) Start(MongoHistoryProcessor? processor = null)
    {
        var cts = new CancellationTokenSource();
        var sut = processor ?? CreateProcessor();
        var task = Task.Run(async () =>
        {
            try
            { await sut.RunAsync(cts.Token); }
            catch (OperationCanceledException) { /* stop requested */ }
        });
        var entry = (cts, task);
        _running.Add(entry);
        return entry;
    }

    private static async Task StopAsync((CancellationTokenSource Cts, Task Task) running)
    {
        running.Cts.Cancel();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    private static async Task<T> WaitForAsync<T>(Func<Task<T>> probe, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        var value = await probe();
        while (!done(value))
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the expected history rows.");
            await Task.Delay(100);
            value = await probe();
        }

        return value;
    }

    private Task<List<HistoryEntry>> WaitForRowsAsync(int count) =>
        WaitForAsync(GetRowsAsync, rows => rows.Count >= count);

    private Task<List<HistoryEntry>> GetRowsAsync() =>
        History.Find(FilterDefinition<HistoryEntry>.Empty).SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken).ToListAsync();

    private static BsonDocument NewOrder(ObjectId id, string status = "NEW") =>
        new() { { "_id", id }, { "SalesOrder", "SO-" + id }, { "StatusOrder", status }, { "CreationDate", DateTime.UtcNow }, { "Total", 10 } };

    [Fact]
    public async Task Insert_ShouldCreatePlusRowWithFullDocument()
    {
        Start();
        var order = NewOrder(ObjectId.GenerateNewId());
        await Orders.InsertOneAsync(order);

        var rows = await WaitForRowsAsync(1);

        var row = Assert.Single(rows);
        Assert.Equal("+", row.HistoryType);
        Assert.Equal(order["_id"], row.EntityId);
        Assert.Equal(order["SalesOrder"], row.Data["SalesOrder"]);
        Assert.False(row.Data.Contains("_id"));
    }

    [Fact]
    public async Task Replace_ChangingOnlyStatus_ShouldCreateDeltaWithOnlyThatField()
    {
        var id = ObjectId.GenerateNewId();
        await Orders.InsertOneAsync(NewOrder(id));
        await SeedResumeTokenAsync(); // history starts after the pre-existing insert
        Start();

        var changed = NewOrder(id, "PAID");
        await Orders.ReplaceOneAsync(new BsonDocument("_id", id), changed);

        var rows = await WaitForRowsAsync(1);
        var row = Assert.Single(rows);
        Assert.Equal("~", row.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" } }, row.Data);
        Assert.Null(row.RemovedFields);
    }

    [Fact]
    public async Task SetUpdate_ShouldCreateDelta_AndIdenticalReplaceShouldCreateNoRow()
    {
        var id = ObjectId.GenerateNewId();
        var original = NewOrder(id);
        await Orders.InsertOneAsync(original);
        await SeedResumeTokenAsync();
        Start();

        await Orders.ReplaceOneAsync(new BsonDocument("_id", id), original); // identical: no row
        await Orders.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("StatusOrder", "SHIPPED")));

        var rows = await WaitForRowsAsync(1);
        await Task.Delay(500);
        rows = await GetRowsAsync();
        var row = Assert.Single(rows);
        Assert.Equal("~", row.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "SHIPPED" } }, row.Data);
    }

    [Fact]
    public async Task RawBulkWrites_ShouldBeCaptured()
    {
        var rawId = ObjectId.GenerateNewId();
        var otherId = ObjectId.GenerateNewId();
        await Orders.InsertOneAsync(new BsonDocument { { "_id", otherId }, { "StatusOrder", "NEW" } });
        await SeedResumeTokenAsync();
        Start();

        await Orders.BulkWriteAsync(new WriteModel<BsonDocument>[]
        {
            new InsertOneModel<BsonDocument>(NewOrder(rawId)),
            new UpdateOneModel<BsonDocument>(new BsonDocument("_id", rawId), new BsonDocument("$set", new BsonDocument("StatusOrder", "PAID"))),
            new UpdateOneModel<BsonDocument>(new BsonDocument("_id", otherId), new BsonDocument("$set", new BsonDocument("AuthorizationCode", "AUTH-1"))),
        });

        var rows = await WaitForRowsAsync(3);

        Assert.Contains(rows, r => r.EntityId == rawId && r.HistoryType == "+");
        Assert.Contains(rows, r => r.EntityId == rawId && r.HistoryType == "~" && r.Data["StatusOrder"].AsString == "PAID");
        Assert.Contains(rows, r => r.EntityId == otherId && r.HistoryType == "~" && r.Data.Contains("AuthorizationCode"));
    }

    [Fact]
    public async Task Replay_ShouldRebuildCurrentDocument()
    {
        Start();
        var id = ObjectId.GenerateNewId();
        await Orders.InsertOneAsync(NewOrder(id));
        await Orders.ReplaceOneAsync(new BsonDocument("_id", id), NewOrder(id, "PAID"));
        await Orders.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("Total", 99)));
        await Orders.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$unset", new BsonDocument("SalesOrder", "")));

        var rows = await WaitForRowsAsync(4);

        var rebuilt = HistoryReplay.Rebuild(rows);
        var current = await Orders.Find(new BsonDocument("_id", id)).FirstAsync();
        current.Remove("_id");
        current.Remove("CreationDate");
        rebuilt.Remove("CreationDate");
        Assert.Equal(current, rebuilt);
        Assert.Equal("PAID", rebuilt["StatusOrder"].AsString);
        Assert.False(rebuilt.Contains("SalesOrder"));
    }

    [Fact]
    public async Task RestartedProcessor_ShouldRecordMissedWriteOnceAndNotDuplicateEarlierOne()
    {
        var first = Start();
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await WaitForRowsAsync(1);
        await StopAsync(first);

        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        Start();

        var rows = await WaitForRowsAsync(2);
        await Task.Delay(1000);
        rows = await GetRowsAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r.ChangeToken).Distinct().Count());
    }

    [Fact]
    public async Task Redelivery_ShouldNotCreateDuplicates()
    {
        var firstToken = (await _checkpoints.GetResumeTokenAsync(CancellationToken.None))!;
        var first = Start();
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await WaitForRowsAsync(2);
        await StopAsync(first);

        // Rewind to the earlier position: both events are delivered again.
        await _checkpoints.SaveResumeTokenAsync(firstToken, CancellationToken.None);
        Start();
        await WaitForAsync(
            async () => (await Checkpoints.Find(new BsonDocument("_id", "orders")).FirstAsync())["ResumeToken"].AsBsonDocument,
            token => token != firstToken);

        var rows = await GetRowsAsync();
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Redelivery_OfABatchWithDuplicatesBeforeANewRow_ShouldStillInsertTheNewRow()
    {
        var firstToken = (await _checkpoints.GetResumeTokenAsync(CancellationToken.None))!;
        var first = Start();
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        var recorded = await WaitForRowsAsync(3);
        await StopAsync(first);

        // The last row goes missing, then the three events are delivered again in one batch: [dup, dup, new]. An
        // ordered insert would stop at the first duplicate and lose the new row.
        await History.DeleteOneAsync(h => h.ChangeToken == recorded[2].ChangeToken);
        await _checkpoints.SaveResumeTokenAsync(firstToken, CancellationToken.None);
        Start();

        var rows = await WaitForRowsAsync(3);
        Assert.Equal(recorded.Select(r => r.ChangeToken), rows.Select(r => r.ChangeToken));
    }

    [Fact]
    public async Task MissingPreImage_ShouldFallBackToSnapshotAndUpdateDescription()
    {
        var id = ObjectId.GenerateNewId();
        await Orders.InsertOneAsync(NewOrder(id));
        await SeedResumeTokenAsync();
        Start();

        await SetPreAndPostImagesAsync(false);
        try
        {
            await Orders.ReplaceOneAsync(new BsonDocument("_id", id), NewOrder(id, "PAID"));
            await Orders.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("Total", 55)));

            var rows = await WaitForRowsAsync(2);

            var snapshot = Assert.Single(rows, r => r.HistoryType == "=");
            Assert.Equal("PAID", snapshot.Data["StatusOrder"].AsString);
            var delta = Assert.Single(rows, r => r.HistoryType == "~");
            Assert.Equal(new BsonDocument { { "Total", 55 } }, delta.Data);
        }
        finally
        {
            await SetPreAndPostImagesAsync(true);
        }
    }

    [Fact]
    public async Task IdleCollection_ShouldAdvanceSavedPosition()
    {
        var before = (await _checkpoints.GetResumeTokenAsync(CancellationToken.None))!;
        Start();

        await Database.GetCollection<BsonDocument>("somethingElse").InsertOneAsync(new BsonDocument("x", 1));

        await WaitForAsync(
            async () => await _checkpoints.GetResumeTokenAsync(CancellationToken.None),
            token => token is not null && token != before);
        Assert.Empty(await GetRowsAsync());
    }

    [Fact]
    public async Task StringGuidAndIntIds_ShouldBeRecordedWithTheirOwnType()
    {
        var guid = Guid.NewGuid();
        var thingsOptions = new MongoHistoryOptions("things");
        await new MongoHistorySetup(Database, thingsOptions).EnsureSetupAsync();
        var things = Database.GetCollection<BsonDocument>("things");
        var thingsStore = new MongoHistoryCheckpointStore(Database, thingsOptions.CheckpointName, thingsOptions.CheckpointId);
        await SeedResumeTokenAsync(things, thingsStore);
        Start(new MongoHistoryProcessor(Database, thingsOptions, thingsStore, new HistoryHealthState(), NullLogger.Instance));
        var thingsHistory = Database.GetCollection<HistoryEntry>("thingsHistory");

        await things.InsertManyAsync(new[]
        {
            new BsonDocument { { "_id", "abc" }, { "V", 1 } },
            new BsonDocument { { "_id", new BsonBinaryData(guid, GuidRepresentation.Standard) }, { "V", 2 } },
            new BsonDocument { { "_id", 42 }, { "V", 3 } },
        });
        await things.UpdateOneAsync(new BsonDocument("_id", 42), new BsonDocument("$set", new BsonDocument("V", 4)));

        var rows = await WaitForAsync(
            () => thingsHistory.Find(FilterDefinition<HistoryEntry>.Empty).SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken).ToListAsync(),
            r => r.Count >= 4);

        Assert.Contains(rows, r => r.EntityId == new BsonString("abc") && r.HistoryType == "+");
        Assert.Contains(rows, r => r.EntityId == new BsonBinaryData(guid, GuidRepresentation.Standard) && r.HistoryType == "+");
        Assert.Contains(rows, r => r.EntityId == new BsonInt32(42) && r.HistoryType == "+");
        var delta = Assert.Single(rows, r => r.HistoryType == "~");
        Assert.Equal(new BsonInt32(42), delta.EntityId);
        Assert.Equal(new BsonDocument("V", 4), delta.Data);
    }

    [Fact]
    public async Task TwoCollections_ShouldRecordIntoTheirOwnHistoryAndKeepIndependentCheckpoints()
    {
        var carts = new MongoHistoryOptions("carts"); // no ignored fields
        await new MongoHistorySetup(Database, carts).EnsureSetupAsync();
        var cartsCollection = Database.GetCollection<BsonDocument>("carts");
        var cartsHistory = Database.GetCollection<HistoryEntry>("cartsHistory");
        var cartsCheckpoints = new MongoHistoryCheckpointStore(Database, carts.CheckpointName, carts.CheckpointId);
        await SeedResumeTokenAsync(cartsCollection, cartsCheckpoints);
        await SeedResumeTokenAsync();

        Start();
        var cartsRun = Start(new MongoHistoryProcessor(Database, carts, cartsCheckpoints, new HistoryHealthState(), NullLogger.Instance));

        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await cartsCollection.InsertOneAsync(new BsonDocument { { "_id", "cart-1" }, { "CreationDate", 1 } });
        await WaitForRowsAsync(1);
        await WaitForAsync(() => cartsHistory.Find(FilterDefinition<HistoryEntry>.Empty).ToListAsync(), r => r.Count >= 1);

        Assert.Single(await GetRowsAsync());
        var cartRow = Assert.Single(await cartsHistory.Find(FilterDefinition<HistoryEntry>.Empty).ToListAsync());
        Assert.Equal(new BsonString("cart-1"), cartRow.EntityId);
        Assert.True(cartRow.Data.Contains("CreationDate")); // carts do not ignore it, orders do
        Assert.Equal(2, await Checkpoints.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

        // Stopping one processor leaves the other's position and work untouched.
        await StopAsync(cartsRun);
        // Read once the carts run has stopped: nothing may move its position afterwards.
        var cartsToken = await cartsCheckpoints.GetResumeTokenAsync(CancellationToken.None);
        var ordersToken = await _checkpoints.GetResumeTokenAsync(CancellationToken.None);
        Assert.NotNull(cartsToken);
        await cartsCollection.InsertOneAsync(new BsonDocument { { "_id", "cart-2" } });
        await Orders.InsertOneAsync(NewOrder(ObjectId.GenerateNewId()));
        await WaitForRowsAsync(2);
        await WaitForAsync(() => _checkpoints.GetResumeTokenAsync(CancellationToken.None), token => token != ordersToken);

        Assert.Single(await cartsHistory.Find(FilterDefinition<HistoryEntry>.Empty).ToListAsync());
        Assert.Equal(cartsToken, await cartsCheckpoints.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FullDocumentMode_ShouldStoreWholeDocumentAndChangedFields()
    {
        var full = new MongoHistoryOptions("fullOrders") { StoreFullDocument = true }.IgnoreFields("CreationDate");
        await new MongoHistorySetup(Database, full).EnsureSetupAsync();
        var source = Database.GetCollection<BsonDocument>("fullOrders");
        var store = new MongoHistoryCheckpointStore(Database, full.CheckpointName, full.CheckpointId);
        await SeedResumeTokenAsync(source, store);
        Start(new MongoHistoryProcessor(Database, full, store, new HistoryHealthState(), NullLogger.Instance));
        var history = Database.GetCollection<HistoryEntry>("fullOrdersHistory");

        var id = ObjectId.GenerateNewId();
        await source.InsertOneAsync(NewOrder(id));
        await source.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$set", new BsonDocument("StatusOrder", "PAID")));
        await source.UpdateOneAsync(new BsonDocument("_id", id), new BsonDocument("$unset", new BsonDocument("Total", "")));

        var rows = await WaitForAsync(
            () => history.Find(FilterDefinition<HistoryEntry>.Empty).SortBy(h => h.HistoryDate).ThenBy(h => h.ChangeToken).ToListAsync(),
            r => r.Count >= 3);

        Assert.Equal<string>(["+", "~", "~"], rows.Select(r => r.HistoryType));
        Assert.Null(rows[0].ChangedFields);
        Assert.Equal<string>(["StatusOrder"], rows[1].ChangedFields);
        Assert.Equal("PAID", rows[1].Data["StatusOrder"].AsString);
        Assert.True(rows[1].Data.Contains("SalesOrder")); // the whole document, not only the changed field
        Assert.True(rows[1].Data.Contains("CreationDate")); // ignored fields are still stored, they just never count as a change
        Assert.Empty(rows[2].ChangedFields!);
        Assert.Equal<string>(["Total"], rows[2].RemovedFields);
        Assert.False(rows[2].Data.Contains("Total"));
        var current = await source.Find(new BsonDocument("_id", id)).FirstAsync();
        current.Remove("_id");
        Assert.Equal(current, HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public async Task BatchSizeOfOne_ShouldStillRecordEveryEvent()
    {
        OrdersOptions.BatchSize = 1;
        Start();

        await Orders.InsertManyAsync(new[] { NewOrder(ObjectId.GenerateNewId()), NewOrder(ObjectId.GenerateNewId()), NewOrder(ObjectId.GenerateNewId()) });

        var rows = await WaitForRowsAsync(3);
        Assert.Equal(3, rows.Select(r => r.ChangeToken).Distinct().Count());
    }

    private Task<BsonDocument> SetPreAndPostImagesAsync(bool enabled) =>
        Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "collMod", "orders" },
            { "changeStreamPreAndPostImages", new BsonDocument("enabled", enabled) },
        });
}
