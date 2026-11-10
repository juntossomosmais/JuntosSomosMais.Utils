using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

public class MongoRequestAuditSetupTests : RequestAuditMongoTestBase
{
    private static readonly string[] IndexNames = ["CreatedAtUtc_1", "IpAddress_1", "RequestId_1", "Url_1", "_id_"];

    [Fact]
    public async Task EnsureAsync_WithoutRetention_CreatesTheIndexesWithoutExpiration()
    {
        // Arrange
        var options = new MongoRequestAuditOptions();

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, options);

        // Assert
        var indexes = await ListIndexesAsync();
        Assert.Equal(IndexNames, indexes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new BsonDocument("CreatedAtUtc", 1), indexes["CreatedAtUtc_1"]["key"]);
        Assert.Equal(new BsonDocument("Url", 1), indexes["Url_1"]["key"]);
        Assert.Equal(new BsonDocument("IpAddress", 1), indexes["IpAddress_1"]["key"]);
        Assert.Equal(new BsonDocument("RequestId", 1), indexes["RequestId_1"]["key"]);
        Assert.False(indexes["CreatedAtUtc_1"].Contains("expireAfterSeconds"));
    }

    [Fact]
    public async Task EnsureAsync_WithRetention_ExpiresTheEntries()
    {
        // Arrange
        var options = new MongoRequestAuditOptions { CollectionName = "customAudit", Retention = TimeSpan.FromDays(30) };

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, options);

        // Assert
        var indexes = await ListIndexesAsync("customAudit");
        Assert.Equal(IndexNames, indexes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(2_592_000, indexes["CreatedAtUtc_1"]["expireAfterSeconds"].ToInt64());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(30)]
    public async Task EnsureAsync_CalledTwiceWithTheSameOptions_ChangesNothing(int? retentionDays)
    {
        // Arrange
        var options = new MongoRequestAuditOptions { Retention = ToRetention(retentionDays) };
        await MongoRequestAuditSetup.EnsureAsync(Database, options);
        var before = await ListIndexesAsync();

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, options);

        // Assert
        Assert.Equal(before, await ListIndexesAsync());
    }

    [Theory]
    [InlineData(30, 60)]
    [InlineData(null, 60)]
    [InlineData(30, null)]
    public async Task EnsureAsync_RetentionChanged_UpdatesTheExpiration(int? currentDays, int? newDays)
    {
        // Arrange
        await MongoRequestAuditSetup.EnsureAsync(Database, new MongoRequestAuditOptions { Retention = ToRetention(currentDays) });

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, new MongoRequestAuditOptions { Retention = ToRetention(newDays) });

        // Assert
        var indexes = await ListIndexesAsync();
        var expiration = indexes["CreatedAtUtc_1"].GetValue("expireAfterSeconds", BsonNull.Value);
        Assert.Equal(IndexNames, indexes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(newDays * 86_400L, expiration.IsBsonNull ? (long?)null : expiration.ToInt64());
    }

    [Fact]
    public async Task EnsureAsync_InvalidOptions_Throws()
    {
        // Arrange
        var options = new MongoRequestAuditOptions { CollectionName = " " };

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MongoRequestAuditSetup.EnsureAsync(Database, options));

        // Assert
        Assert.Equal("CollectionName is required.", exception.Message);
    }

    [Fact]
    public async Task EnsureAsync_NullArguments_Throws()
    {
        // Arrange
        var options = new MongoRequestAuditOptions();

        // Act
        var withoutDatabase = await Assert.ThrowsAsync<ArgumentNullException>(() => MongoRequestAuditSetup.EnsureAsync(null!, options));
        var withoutOptions = await Assert.ThrowsAsync<ArgumentNullException>(() => MongoRequestAuditSetup.EnsureAsync(Database, null!));

        // Assert
        Assert.Equal("database", withoutDatabase.ParamName);
        Assert.Equal("options", withoutOptions.ParamName);
    }

    private static TimeSpan? ToRetention(int? days) => days is null ? null : TimeSpan.FromDays(days.Value);

    private async Task<Dictionary<string, BsonDocument>> ListIndexesAsync(string collection = "requestAuditLogs")
    {
        using var cursor = await Collection(collection).Indexes.ListAsync();
        return (await cursor.ToListAsync()).ToDictionary(index => index["name"].AsString);
    }
}
