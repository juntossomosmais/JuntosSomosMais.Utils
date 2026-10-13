using System.Net;
using System.Reflection;
using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryProcessorTests
{
    private MongoHistoryOptions Options { get; } = new("myOrders");

    private readonly Mock<IMongoCollection<BsonDocument>> _source = new();
    private readonly Mock<IMongoCollection<HistoryEntry>> _history = new();
    private readonly Mock<IHistoryCheckpointStore> _checkpoints = new();
    private readonly Mock<ILogger> _logger = new();
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly HistoryHealthState _state;
    private readonly List<List<HistoryEntry>> _insertedBatches = new();
    private ChangeStreamOptions? _capturedOptions;

    public MongoHistoryProcessorTests()
    {
        _state = new HistoryHealthState(_clock);
        _history
            .Setup(h => h.InsertManyAsync(It.IsAny<IEnumerable<HistoryEntry>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<HistoryEntry>, InsertManyOptions, CancellationToken>((rows, _, _) => _insertedBatches.Add(rows.ToList()))
            .Returns(Task.CompletedTask);
    }

    private MongoHistoryProcessor CreateSut() =>
        new(_source.Object, _history.Object, Options, _checkpoints.Object, _state, _logger.Object);

    private void SetupWatch(FakeChangeStreamCursor cursor) =>
        _source
            .Setup(o => o.WatchAsync(
                It.IsAny<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>(),
                It.IsAny<ChangeStreamOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>, ChangeStreamOptions, CancellationToken>(
                (_, options, _) => _capturedOptions = options)
            .ReturnsAsync(cursor);

    private static ChangeStreamDocument<BsonDocument> InsertEvent(BsonValue id, string token) => Event("insert", id, token,
        new BsonDocument { { "fullDocument", new BsonDocument { { "_id", id }, { "StatusOrder", "NEW" } } } });

    private static ChangeStreamDocument<BsonDocument> Event(string operation, BsonValue id, string token, BsonDocument extra)
    {
        var backing = new BsonDocument
        {
            { "_id", new BsonDocument("_data", token) },
            { "operationType", operation },
            { "clusterTime", new BsonTimestamp(1_790_000_000, 1) },
            { "wallTime", new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc) },
            { "ns", new BsonDocument { { "db", "sales_test" }, { "coll", "myOrders" } } },
            { "documentKey", new BsonDocument("_id", id) },
        };
        backing.Merge(extra);
        return new ChangeStreamDocument<BsonDocument>(backing, BsonSerializer.LookupSerializer<BsonDocument>());
    }

    private static bool HasLogValue(object state, string key, object expected) =>
        state is IReadOnlyList<KeyValuePair<string, object?>> values
        && values.Any(v => v.Key == key && Equals(v.Value, expected));

    private static MongoBulkWriteException<HistoryEntry> BulkException(params (ServerErrorCategory Category, int Code)[] errors)
    {
        var ctor = typeof(BulkWriteError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single();
        var writeErrors = errors
            .Select((e, index) => (BulkWriteError)ctor.Invoke(new object?[] { index, e.Category, e.Code, "error", new BsonDocument() }))
            .ToList();
        var connectionId = new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017)));
        return new MongoBulkWriteException<HistoryEntry>(
            connectionId,
            new BulkWriteResult<HistoryEntry>.Unacknowledged(errors.Length, new List<InsertOneModel<HistoryEntry>>()),
            writeErrors,
            null,
            Array.Empty<WriteModel<HistoryEntry>>());
    }

    [Fact]
    public async Task RunAsync_ShouldOpenStreamWithImagesAndSavedToken()
    {
        var saved = new BsonDocument("_data", "saved");
        _checkpoints.Setup(c => c.GetResumeTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        SetupWatch(new FakeChangeStreamCursor());

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.NotNull(_capturedOptions);
        Assert.Equal(ChangeStreamFullDocumentOption.WhenAvailable, _capturedOptions!.FullDocument);
        Assert.Equal(ChangeStreamFullDocumentBeforeChangeOption.WhenAvailable, _capturedOptions.FullDocumentBeforeChange);
        Assert.Equal(saved, _capturedOptions.StartAfter);
        Assert.Equal(TimeSpan.FromSeconds(5), _capturedOptions.MaxAwaitTime);
        Assert.Equal(100, _capturedOptions.BatchSize);
    }

    [Fact]
    public async Task RunAsync_WhenBatchHasThreeEvents_ShouldInsertOnceAndCheckpointOnce()
    {
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "after-batch"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            InsertEvent(ObjectId.GenerateNewId(), "t1"),
            InsertEvent(ObjectId.GenerateNewId(), "t2"),
            InsertEvent(ObjectId.GenerateNewId(), "t3"),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        var batch = Assert.Single(_insertedBatches);
        Assert.Equal<string>(["t1", "t2", "t3"], batch.Select(r => r.ChangeToken));
        Assert.All(batch, r => Assert.Equal("+", r.HistoryType));
        _checkpoints.Verify(c => c.SaveResumeTokenAsync(new BsonDocument("_data", "after-batch"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenBatchIsEmpty_ShouldStillSaveToken()
    {
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "idle"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>());
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.Empty(_insertedBatches);
        _checkpoints.Verify(c => c.SaveResumeTokenAsync(new BsonDocument("_data", "idle"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_AfterSavingTheToken_ShouldMarkTheCheckpointWithTheStatesClock()
    {
        // Arrange: the save takes a second on the clock, so the mark must be read after it.
        _checkpoints.Setup(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()))
            .Callback(() => _clock.Advance(TimeSpan.FromSeconds(1)))
            .Returns(Task.CompletedTask);
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "idle"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>());
        SetupWatch(cursor);

        // Act
        await CreateSut().RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(Now.AddSeconds(1), _state.LastCheckpointUtc);
    }

    [Fact]
    public async Task RunAsync_WhenSavingTheTokenFails_ShouldNotMarkTheCheckpoint()
    {
        _checkpoints.Setup(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo down"));
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "idle"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>());
        SetupWatch(cursor);

        await Assert.ThrowsAsync<TimeoutException>(() => CreateSut().RunAsync(CancellationToken.None));

        Assert.Null(_state.LastCheckpointUtc);
    }

    [Fact]
    public async Task RunAsync_WhenEventsAreNotHandledOrProduceNoRow_ShouldNotInsert()
    {
        var id = ObjectId.GenerateNewId();
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("drop", id, "d1", new BsonDocument()),
            Event("replace", id, "r1", new BsonDocument
            {
                { "fullDocument", new BsonDocument { { "_id", id }, { "A", 1 } } },
                { "fullDocumentBeforeChange", new BsonDocument { { "_id", id }, { "A", 1 } } },
            }),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.Empty(_insertedBatches);
    }

    [Fact]
    public async Task RunAsync_WhenUpdateEvent_ShouldBuildRowFromUpdateDescription()
    {
        var id = ObjectId.GenerateNewId();
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("update", id, "u1", new BsonDocument
            {
                { "fullDocument", new BsonDocument { { "_id", id }, { "StatusOrder", "PAID" }, { "Items", new BsonArray { 1 } } } },
                {
                    "updateDescription", new BsonDocument
                    {
                        { "updatedFields", new BsonDocument("StatusOrder", "PAID") },
                        { "removedFields", new BsonArray { "Gone" } },
                        { "truncatedArrays", new BsonArray { new BsonDocument { { "field", "Items" }, { "newSize", 1 } } } },
                    }
                },
            }),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        var row = Assert.Single(Assert.Single(_insertedBatches));
        Assert.Equal("~", row.HistoryType);
        Assert.Equal(new BsonDocument { { "Items", new BsonArray { 1 } }, { "StatusOrder", "PAID" } }, row.Data);
        Assert.Equal<string>(["Gone"], row.RemovedFields);
        Assert.Equal(new BsonObjectId(id), row.EntityId);
    }

    [Fact]
    public async Task RunAsync_WhenUpdateHasNoPostImage_ShouldLoadCurrentDocumentsWithOneQuery()
    {
        var id = ObjectId.GenerateNewId();
        var otherId = ObjectId.GenerateNewId();
        var current = new BsonDocument { { "_id", id }, { "Products", new BsonArray { new BsonDocument("Status", "OK") } } };
        var lookup = new Mock<IAsyncCursor<BsonDocument>>();
        lookup.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        lookup.SetupGet(c => c.Current).Returns(new List<BsonDocument> { current });
        _source
            .Setup(o => o.FindAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup.Object);
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("update", id, "u1", new BsonDocument("updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("Products.0.Status", "OK") }, { "removedFields", new BsonArray() } })),
            Event("update", id, "u2", new BsonDocument("updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("Products.0.Status", "OK") }, { "removedFields", new BsonArray() } })),
            Event("update", otherId, "u3", new BsonDocument("updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("Total", 1) }, { "removedFields", new BsonArray() } })),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        _source.Verify(o => o.FindAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), It.IsAny<CancellationToken>()), Times.Once);
        var batch = Assert.Single(_insertedBatches);
        Assert.Equal<string>(["u1", "u2", "u3"], batch.Select(r => r.ChangeToken));
        Assert.All(batch.Take(2), r => Assert.Equal(new BsonDocument("Products", new BsonArray { new BsonDocument("Status", "OK") }), r.Data));
        // u3: the document no longer exists, so only the top-level values of the update description are recorded.
        Assert.Equal(new BsonDocument("Total", 1), batch[2].Data);
    }

    [Fact]
    public async Task RunAsync_WhenInsertFailsWithOnlyDuplicateKeyErrors_ShouldSwallowAndContinue()
    {
        _history
            .Setup(h => h.InsertManyAsync(It.IsAny<IEnumerable<HistoryEntry>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(BulkException((ServerErrorCategory.DuplicateKey, 11000), (ServerErrorCategory.DuplicateKey, 11000)));
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>> { InsertEvent(ObjectId.GenerateNewId(), "t1") });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        _checkpoints.Verify(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Information, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) =>
                state.ToString() == "Skipped 2 already-recorded myOrders history rows"
                && HasLogValue(state, "SkippedCount", 2)
                && HasLogValue(state, "SourceName", "myOrders")),
            It.Is<Exception?>(e => e is MongoBulkWriteException<HistoryEntry>),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenInsertFailsWithNonDuplicateError_ShouldPropagateAndNotCheckpoint()
    {
        _history
            .Setup(h => h.InsertManyAsync(It.IsAny<IEnumerable<HistoryEntry>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(BulkException((ServerErrorCategory.Uncategorized, 121)));
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>> { InsertEvent(ObjectId.GenerateNewId(), "t1") });
        SetupWatch(cursor);

        await Assert.ThrowsAsync<MongoBulkWriteException<HistoryEntry>>(() => CreateSut().RunAsync(CancellationToken.None));

        _checkpoints.Verify(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenInsertFailsWithDuplicateAndOtherError_ShouldPropagateAndNotCheckpoint()
    {
        _history
            .Setup(h => h.InsertManyAsync(It.IsAny<IEnumerable<HistoryEntry>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(BulkException((ServerErrorCategory.DuplicateKey, 11000), (ServerErrorCategory.Uncategorized, 121)));
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>> { InsertEvent(ObjectId.GenerateNewId(), "t1"), InsertEvent(ObjectId.GenerateNewId(), "t2") });
        SetupWatch(cursor);

        await Assert.ThrowsAsync<MongoBulkWriteException<HistoryEntry>>(() => CreateSut().RunAsync(CancellationToken.None));

        _checkpoints.Verify(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenStreamCloses_ShouldKeepResumeToken()
    {
        // The last batch of a dropped collection: drop, then invalidate; the server then closes the cursor.
        var id = ObjectId.GenerateNewId();
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "invalidate"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("drop", id, "d1", new BsonDocument()),
            Event("invalidate", id, "i1", new BsonDocument()),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.Empty(_insertedBatches);
        _checkpoints.Verify(c => c.SaveResumeTokenAsync(new BsonDocument("_data", "invalidate"), It.IsAny<CancellationToken>()), Times.Once);
        _checkpoints.Verify(c => c.SaveResumeTokenAsync(It.IsAny<BsonDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        _checkpoints.Verify(c => c.ClearResumeTokenAsync(It.IsAny<BsonDocument?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenHistoryIsLost_ShouldLogGapAndClearTokenWithoutThrowing()
    {
        var startToken = new BsonDocument("_data", "start");
        _checkpoints.Setup(c => c.GetResumeTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(startToken);
        _source
            .Setup(o => o.WatchAsync(
                It.IsAny<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>(),
                It.IsAny<ChangeStreamOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CommandException(286));

        await CreateSut().RunAsync(CancellationToken.None);

        // Only the position this run started from may be cleared.
        _checkpoints.Verify(c => c.ClearResumeTokenAsync(startToken, It.IsAny<CancellationToken>()), Times.Once);
        _checkpoints.Verify(c => c.ClearResumeTokenAsync(It.IsAny<BsonDocument?>(), It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("history gap")),
            It.Is<Exception?>(e => e is MongoCommandException && ((MongoCommandException)e).Code == 286),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenHistoryIsLostAfterABatch_ShouldClearOnlyTheTokenThisRunSaved()
    {
        _checkpoints.Setup(c => c.GetResumeTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new BsonDocument("_data", "start"));
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "after-batch")) { ThrowAfterBatches = CommandException(286) };
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>());
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        _checkpoints.Verify(c => c.ClearResumeTokenAsync(new BsonDocument("_data", "after-batch"), It.IsAny<CancellationToken>()), Times.Once);
        _checkpoints.Verify(c => c.ClearResumeTokenAsync(It.IsAny<BsonDocument?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenOtherCommandErrorOccurs_ShouldPropagate()
    {
        _source
            .Setup(o => o.WatchAsync(
                It.IsAny<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>(),
                It.IsAny<ChangeStreamOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CommandException(13));

        await Assert.ThrowsAsync<MongoCommandException>(() => CreateSut().RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_ShouldPropagateAndKeepToken()
    {
        using var cts = new CancellationTokenSource();
        var cursor = new FakeChangeStreamCursor { CancelOnMoveNext = cts };
        SetupWatch(cursor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().RunAsync(cts.Token));

        _checkpoints.Verify(c => c.ClearResumeTokenAsync(It.IsAny<BsonDocument?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<BsonValue, BsonValue> EntityIds() => new()
    {
        { new BsonString("order-1"), new BsonString("order-2") },
        { new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard), new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
        { new BsonInt32(7), new BsonInt32(8) },
    };

    [Theory]
    [MemberData(nameof(EntityIds))]
    public async Task RunAsync_WithAnyIdType_ShouldKeepEntityIdAndFillMissingPostImagesWithOneQuery(BsonValue id, BsonValue otherId)
    {
        var current = new BsonDocument { { "_id", id }, { "Status", "OK" } };
        var lookup = new Mock<IAsyncCursor<BsonDocument>>();
        lookup.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        lookup.SetupGet(c => c.Current).Returns(new List<BsonDocument> { current });
        _source
            .Setup(o => o.FindAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lookup.Object);
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("update", id, "u1", new BsonDocument("updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("Status", "OK") }, { "removedFields", new BsonArray() } })),
            Event("update", otherId, "u2", new BsonDocument("updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("Status", "OK") }, { "removedFields", new BsonArray() } })),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        _source.Verify(o => o.FindAsync(It.IsAny<FilterDefinition<BsonDocument>>(), It.IsAny<FindOptions<BsonDocument, BsonDocument>>(), It.IsAny<CancellationToken>()), Times.Once);
        // otherId no longer exists: its row comes from the update description alone.
        var rows = Assert.Single(_insertedBatches);
        Assert.Equal([id, otherId], rows.Select(r => r.EntityId));
        Assert.All(rows, r => Assert.Equal(new BsonDocument("Status", "OK"), r.Data));
    }

    [Fact]
    public async Task RunAsync_ShouldUseOptionsValues()
    {
        Options.BatchSize = 1;
        Options.MaxAwaitTime = TimeSpan.FromSeconds(2);
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>());
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.Equal(1, _capturedOptions!.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(2), _capturedOptions.MaxAwaitTime);
    }

    [Fact]
    public async Task RunAsync_ShouldHonourTheIgnoredFieldsOfItsOwnOptions()
    {
        Options.IgnoreFields("CreationDate");
        var id = ObjectId.GenerateNewId();
        var cursor = new FakeChangeStreamCursor(new BsonDocument("_data", "x"));
        cursor.Batches.Add(new List<ChangeStreamDocument<BsonDocument>>
        {
            Event("update", id, "u1", new BsonDocument
            {
                { "fullDocument", new BsonDocument { { "_id", id }, { "CreationDate", 5 } } },
                { "fullDocumentBeforeChange", new BsonDocument { { "_id", id }, { "CreationDate", 1 } } },
                { "updateDescription", new BsonDocument { { "updatedFields", new BsonDocument("CreationDate", 5) }, { "removedFields", new BsonArray() } } },
            }),
        });
        SetupWatch(cursor);

        await CreateSut().RunAsync(CancellationToken.None);

        Assert.Empty(_insertedBatches);
    }

    [Fact]
    public async Task RunAsync_WhenHistoryIsLost_ShouldNameTheSourceInTheLog()
    {
        var sut = new MongoHistoryProcessor(_source.Object, _history.Object, new MongoHistoryOptions("carts"), _checkpoints.Object, _state, _logger.Object);
        _source
            .Setup(o => o.WatchAsync(
                It.IsAny<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>(),
                It.IsAny<ChangeStreamOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CommandException(286));

        await sut.RunAsync(CancellationToken.None);

        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.StartsWith("carts history gap")),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithDatabase_ShouldResolveCollectionsFromOptions()
    {
        var database = new Mock<IMongoDatabase>();
        database.Setup(d => d.GetCollection<BsonDocument>("carts", null)).Returns(_source.Object);
        database.Setup(d => d.GetCollection<HistoryEntry>("cartsHistory", null)).Returns(_history.Object);

        _ = new MongoHistoryProcessor(database.Object, new MongoHistoryOptions("carts"), _checkpoints.Object, _state, _logger.Object);

        database.Verify(d => d.GetCollection<BsonDocument>("carts", null), Times.Once);
        database.Verify(d => d.GetCollection<HistoryEntry>("cartsHistory", null), Times.Once);
    }

    private static MongoCommandException CommandException(int code)
    {
        var connectionId = new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017)));
        return new MongoCommandException(connectionId, "command failed", new BsonDocument("find", "x"), new BsonDocument { { "ok", 0 }, { "code", code } });
    }

    private sealed class FakeChangeStreamCursor : IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>
    {
        private readonly BsonDocument _token;
        private int _index = -1;

        public FakeChangeStreamCursor(BsonDocument? token = null) => _token = token ?? new BsonDocument("_data", "default");

        public List<List<ChangeStreamDocument<BsonDocument>>> Batches { get; } = new();
        public int MoveNextCalls { get; private set; }
        public CancellationTokenSource? CancelOnMoveNext { get; init; }
        public Exception? ThrowAfterBatches { get; init; }

        public IEnumerable<ChangeStreamDocument<BsonDocument>> Current => _index >= 0 && _index < Batches.Count
            ? Batches[_index]
            : Enumerable.Empty<ChangeStreamDocument<BsonDocument>>();

        public BsonDocument GetResumeToken() => _token;

        public bool MoveNext(CancellationToken cancellationToken = default) => MoveNextAsync(cancellationToken).GetAwaiter().GetResult();

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
        {
            MoveNextCalls++;
            if (CancelOnMoveNext is not null)
            {
                CancelOnMoveNext.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            _index++;
            return _index >= Batches.Count && ThrowAfterBatches is not null
                ? Task.FromException<bool>(ThrowAfterBatches)
                : Task.FromResult(_index < Batches.Count);
        }

        public void Dispose()
        {
        }
    }
}
