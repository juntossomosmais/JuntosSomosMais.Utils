using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

public class MongoRequestAuditSetupTests : RequestAuditMongoTestBase
{
    private static readonly string[] IndexNames = ["CreatedAtUtc_1", "IpAddress_1", "RequestId_1", "Url_1", "_id_"];

    [Theory]
    [InlineData("requestAuditLogs")]
    [InlineData("customAudit")]
    public async Task EnsureAsync_NewCollection_CreatesTheIndexesWithoutExpiration(string collectionName)
    {
        // Arrange
        var options = new MongoRequestAuditOptions { CollectionName = collectionName };

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, options);

        // Assert
        var indexes = await ListIndexesAsync(collectionName);
        Assert.Equal(IndexNames, indexes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new BsonDocument("CreatedAtUtc", 1), indexes["CreatedAtUtc_1"]["key"]);
        Assert.Equal(new BsonDocument("Url", 1), indexes["Url_1"]["key"]);
        Assert.Equal(new BsonDocument("IpAddress", 1), indexes["IpAddress_1"]["key"]);
        Assert.Equal(new BsonDocument("RequestId", 1), indexes["RequestId_1"]["key"]);
        Assert.DoesNotContain(indexes.Values, index => index.Contains("expireAfterSeconds"));
    }

    [Fact]
    public async Task EnsureAsync_CalledTwice_ChangesNothing()
    {
        // Arrange
        var options = new MongoRequestAuditOptions();
        await MongoRequestAuditSetup.EnsureAsync(Database, options);
        var before = await ListIndexesAsync();

        // Act
        await MongoRequestAuditSetup.EnsureAsync(Database, options);

        // Assert
        Assert.Equal(before, await ListIndexesAsync());
    }

    [Fact]
    public async Task EnsureAsync_InvalidOptions_Throws()
    {
        // Arrange
        var options = new MongoRequestAuditOptions { CollectionName = " " };

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => MongoRequestAuditSetup.EnsureAsync(Database, options));

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

    private async Task<Dictionary<string, BsonDocument>> ListIndexesAsync(string collection = "requestAuditLogs")
    {
        using var cursor = await Collection(collection).Indexes.ListAsync();
        return (await cursor.ToListAsync()).ToDictionary(index => index["name"].AsString);
    }
}
