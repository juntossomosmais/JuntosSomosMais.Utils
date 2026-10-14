using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryCheckpointStoreTests : MongoTestBase
{
    private readonly MongoHistoryCheckpointStore _sut;

    public MongoHistoryCheckpointStoreTests()
    {
        _sut = new MongoHistoryCheckpointStore(Database, "simpleHistoryCheckpoint", "myOrders");
    }

    private IMongoCollection<BsonDocument> Checkpoints => Database.GetCollection<BsonDocument>("simpleHistoryCheckpoint");

    [Fact]
    public async Task ResumeToken_ShouldRoundTrip()
    {
        var token = new BsonDocument("_data", "826A00");

        await _sut.SaveResumeTokenAsync(token, CancellationToken.None);

        Assert.Equal(token, await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetResumeTokenAsync_WhenNothingSaved_ShouldReturnNull()
    {
        Assert.Null(await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveResumeTokenAsync_WhenNoDocument_ShouldCreateIt()
    {
        Assert.Equal(0, await Checkpoints.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));

        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);

        var document = Assert.Single(await Checkpoints.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.Equal("myOrders", document["_id"].AsString);
        Assert.Equal(new BsonDocument("_data", "AAA"), document["ResumeToken"].AsBsonDocument);
    }

    [Fact]
    public async Task SaveResumeTokenAsync_WhenCalledTwice_ShouldOverwriteWithoutDuplicatingTheDocument()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);

        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "BBB"), CancellationToken.None);

        Assert.Equal(1, await Checkpoints.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(new BsonDocument("_data", "BBB"), await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveResumeTokenAsync_ShouldStampTheServerTimeOfTheSave()
    {
        var before = DateTime.UtcNow.AddMinutes(-1);

        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);

        var document = await Checkpoints.Find(new BsonDocument("_id", "myOrders")).FirstAsync();
        Assert.InRange(document["SavedAt"].ToUniversalTime(), before, DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task RecordGapAsync_WhenTheStoredTokenIsTheExpectedOne_ShouldRemoveItAndRecordTheGapSinceItsSave()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);
        var savedAt = (await Checkpoints.Find(new BsonDocument("_id", "myOrders")).FirstAsync())["SavedAt"].ToUniversalTime();

        var gap = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);

        Assert.NotNull(gap);
        Assert.Equal(savedAt, gap!.FromUtc);
        Assert.True(gap.ToUtc >= savedAt);
        Assert.Equal(gap, await _sut.GetLastGapAsync(CancellationToken.None));
        Assert.Null(await _sut.GetResumeTokenAsync(CancellationToken.None));
        Assert.NotNull(await _sut.GetRestartAtAsync(CancellationToken.None));
        Assert.Equal(1, await Checkpoints.CountDocumentsAsync(new BsonDocument("_id", "myOrders")));
    }

    [Fact]
    public async Task RecordGapAsync_WhenTheTokenWasSavedWithoutATime_ShouldRecordAGapWithAnUnknownStart()
    {
        // A checkpoint written before SavedAt existed.
        await Checkpoints.InsertOneAsync(new BsonDocument { { "_id", "myOrders" }, { "ResumeToken", new BsonDocument("_data", "AAA") } });

        var gap = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);

        Assert.NotNull(gap);
        Assert.Null(gap!.FromUtc);
    }

    [Fact]
    public async Task RecordGapAsync_WhenADifferentTokenIsStored_ShouldKeepItAndRecordNothing()
    {
        // A stale consumer (expects AAA) must not wipe the newer position another one saved.
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "BBB"), CancellationToken.None);

        var gap = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);

        Assert.Null(gap);
        Assert.Null(await _sut.GetLastGapAsync(CancellationToken.None));
        Assert.Equal(new BsonDocument("_data", "BBB"), await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordGapAsync_WhenExpectedTokenIsNull_ShouldKeepAStoredTokenAndRecordNothing()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "BBB"), CancellationToken.None);

        Assert.Null(await _sut.RecordGapAsync(null, null, CancellationToken.None));

        Assert.Equal(new BsonDocument("_data", "BBB"), await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordGapAsync_WhenNoDocument_ShouldNotCreateOne()
    {
        Assert.Null(await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None));

        Assert.Equal(0, await Checkpoints.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task RecordGapAsync_WhenTheRestartTimeIsTheExpectedOne_ShouldKeepTheGapStartAndMoveTheRestartTime()
    {
        // The stream could not start at the restart time either: the gap grows, from the same start.
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);
        var first = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);
        var firstRestartAt = (await _sut.GetRestartAtAsync(CancellationToken.None))!;

        var second = await _sut.RecordGapAsync(null, firstRestartAt, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(first!.FromUtc, second!.FromUtc);
        Assert.True(second.ToUtc >= first.ToUtc);
        Assert.True((await _sut.GetRestartAtAsync(CancellationToken.None))! > firstRestartAt);
        Assert.Null(await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordGapAsync_WhenADifferentRestartTimeIsStored_ShouldRecordNothing()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);
        var gap = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);
        var restartAt = await _sut.GetRestartAtAsync(CancellationToken.None);

        Assert.Null(await _sut.RecordGapAsync(null, new BsonTimestamp(1, 1), CancellationToken.None));

        Assert.Equal(restartAt, await _sut.GetRestartAtAsync(CancellationToken.None));
        Assert.Equal(gap, await _sut.GetLastGapAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordGapAsync_WhenATokenWasSavedSinceTheRestartTime_ShouldKeepItAndRecordNothing()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);
        await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);
        var restartAt = await _sut.GetRestartAtAsync(CancellationToken.None);
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "BBB"), CancellationToken.None);

        Assert.Null(await _sut.RecordGapAsync(null, restartAt, CancellationToken.None));

        Assert.Equal(new BsonDocument("_data", "BBB"), await _sut.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetRestartAtAsync_WhenNoGapWasRecorded_ShouldReturnNull()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);

        Assert.Null(await _sut.GetRestartAtAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetLastGapAsync_WhenNoGapWasRecorded_ShouldReturnNull()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);

        Assert.Null(await _sut.GetLastGapAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveResumeTokenAsync_AfterAGap_ShouldClearTheRestartTimeAndKeepTheGapForTheHealthWindow()
    {
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "AAA"), CancellationToken.None);
        var gap = await _sut.RecordGapAsync(new BsonDocument("_data", "AAA"), null, CancellationToken.None);

        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "BBB"), CancellationToken.None);

        Assert.Null(await _sut.GetRestartAtAsync(CancellationToken.None));
        Assert.Equal(gap, await _sut.GetLastGapAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TwoStoresWithDifferentCheckpointIds_ShouldBeIndependent()
    {
        var other = new MongoHistoryCheckpointStore(Database, "simpleHistoryCheckpoint", "carts");

        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "ORDERS"), CancellationToken.None);
        await other.SaveResumeTokenAsync(new BsonDocument("_data", "CARTS"), CancellationToken.None);

        Assert.Equal(new BsonDocument("_data", "ORDERS"), await _sut.GetResumeTokenAsync(CancellationToken.None));
        Assert.Equal(new BsonDocument("_data", "CARTS"), await other.GetResumeTokenAsync(CancellationToken.None));
        Assert.Equal(2, await Checkpoints.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task RecordingAGapOnOneSource_ShouldKeepTheOthersToken()
    {
        var other = new MongoHistoryCheckpointStore(Database, "simpleHistoryCheckpoint", "carts");
        await _sut.SaveResumeTokenAsync(new BsonDocument("_data", "A"), CancellationToken.None);
        await other.SaveResumeTokenAsync(new BsonDocument("_data", "B"), CancellationToken.None);

        await _sut.RecordGapAsync(new BsonDocument("_data", "A"), null, CancellationToken.None);

        Assert.Null(await _sut.GetResumeTokenAsync(CancellationToken.None));
        Assert.NotNull(await other.GetResumeTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CustomCollectionName_ShouldBeUsed()
    {
        var custom = new MongoHistoryCheckpointStore(Database, "my_checkpoints", "x");

        await custom.SaveResumeTokenAsync(new BsonDocument("_data", "A"), CancellationToken.None);

        Assert.Equal(1, await Database.GetCollection<BsonDocument>("my_checkpoints").CountDocumentsAsync(new BsonDocument("_id", "x")));
    }
}
