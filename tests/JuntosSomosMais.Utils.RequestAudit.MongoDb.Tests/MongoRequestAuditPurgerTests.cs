using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

public class MongoRequestAuditPurgerTests : RequestAuditMongoTestBase
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly int[] ExpectedIdsPerDelete = [2, 2, 1];

    private readonly MongoRequestAuditPurger _purger;

    public MongoRequestAuditPurgerTests()
    {
        _purger = new MongoRequestAuditPurger(Database, new MongoRequestAuditOptions(), new PurgeClock(Now));
    }

    [Fact]
    public async Task PurgeAsync_Interval_DeletesFromTheLowerBoundUpToButNotTheUpperBound()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2), Day(3), Day(4), Day(5));

        // Act
        var result = await _purger.PurgeAsync(new RequestAuditPurgeRequest(Day(2), Day(4), DryRun: false));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(2, 2, false, Day(4)), result);
        Assert.Equal(new[] { Day(1), Day(4), Day(5) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_OnlyUpperBound_DeletesEverythingOlderInBatches()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2), Day(3), Day(4), Day(5));

        // Act
        var result = await _purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(5), DryRun: false, BatchSize: 2));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(4, 4, false, Day(5)), result);
        Assert.Equal(new[] { Day(5) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_MoreEntriesThanTheBatchSize_DeletesAtMostBatchSizePerCommand()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2), Day(3), Day(4), Day(5));
        var idsPerDelete = new ConcurrentQueue<int>();
        var settings = CreateClientSettings();
        settings.ClusterConfigurator = cluster => cluster.Subscribe<CommandStartedEvent>(started =>
        {
            if (started.CommandName == "delete")
                idsPerDelete.Enqueue(started.Command["deletes"][0]["q"]["_id"]["$in"].AsBsonArray.Count);
        });
        var database = new MongoClient(settings).GetDatabase(Database.DatabaseNamespace.DatabaseName);
        var purger = new MongoRequestAuditPurger(database, new MongoRequestAuditOptions(), new PurgeClock(Now));

        // Act
        var result = await purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(6), DryRun: false, BatchSize: 2));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(5, 5, false, Day(6)), result);
        Assert.Equal(ExpectedIdsPerDelete, idsPerDelete);
        Assert.Empty(await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_OnlyLowerBound_StopsAtTheStartOfThePurge()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2), Now, Now.AddMinutes(1));

        // Act
        var result = await _purger.PurgeAsync(new RequestAuditPurgeRequest(Day(2), null, DryRun: false));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(1, 1, false, Now), result);
        Assert.Equal(new[] { Day(1), Now, Now.AddMinutes(1) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_DryRun_CountsAndDeletesNothing()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2), Day(3));

        // Act
        var result = await _purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(3), DryRun: true));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(2, 0, true, Day(3)), result);
        Assert.Equal(new[] { Day(1), Day(2), Day(3) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_LocalDates_AreConvertedToUtc()
    {
        // Arrange
        await InsertAsync(Day(1), Day(2));

        // Act
        var result = await _purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(2).ToLocalTime(), DryRun: false));

        // Assert
        Assert.Equal(new RequestAuditPurgeResult(1, 1, false, Day(2)), result);
        Assert.Equal(new[] { Day(2) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_CustomCollection_PurgesThatCollection()
    {
        // Arrange
        var purger = new MongoRequestAuditPurger(Database, new MongoRequestAuditOptions { CollectionName = "customAudit" });
        await InsertAsync(Day(1));
        await Collection("customAudit").InsertOneAsync(new BsonDocument("CreatedAtUtc", Day(1)));

        // Act
        var result = await purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(2), DryRun: false));

        // Assert
        Assert.Equal(1, result.Deleted);
        Assert.Equal(0, await Collection("customAudit").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(new[] { Day(1) }, await ReadDatesAsync());
    }

    [Fact]
    public async Task PurgeAsync_InvalidRequests_ThrowAndDeleteNothing()
    {
        // Arrange
        await InsertAsync(Day(1));
        var unspecified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var noBound = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(null, null, false)));
        var batch = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(null, Day(2), false, 0)));
        var inverted = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(Day(2), Day(2), false)));
        var future = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(Now, null, false)));
        var kindFrom = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(unspecified, Day(2), false)));
        var kindTo = await Assert.ThrowsAsync<ArgumentException>(() => _purger.PurgeAsync(new RequestAuditPurgeRequest(null, unspecified, false)));
        var missing = await Assert.ThrowsAsync<ArgumentNullException>(() => _purger.PurgeAsync(null!));

        // Assert
        Assert.StartsWith("At least one of FromUtc or ToUtc is required: the purger never deletes every entry.", noBound.Message);
        Assert.StartsWith("BatchSize must be positive.", batch.Message);
        Assert.StartsWith("FromUtc must be before ToUtc.", inverted.Message);
        Assert.StartsWith("FromUtc must be before the start of the purge.", future.Message);
        Assert.StartsWith("FromUtc must have DateTimeKind Utc or Local, not Unspecified.", kindFrom.Message);
        Assert.StartsWith("ToUtc must have DateTimeKind Utc or Local, not Unspecified.", kindTo.Message);
        Assert.Equal("request", missing.ParamName);
        Assert.Equal(new[] { Day(1) }, await ReadDatesAsync());
    }

    private static DateTime Day(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

    private Task InsertAsync(params DateTime[] dates) =>
        Collection().InsertManyAsync(dates.Select(date => new BsonDocument("CreatedAtUtc", date)));

    private async Task<DateTime[]> ReadDatesAsync() =>
        (await Collection().Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
        .Select(document => document["CreatedAtUtc"].ToUniversalTime())
        .Order()
        .ToArray();

    private sealed class PurgeClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
