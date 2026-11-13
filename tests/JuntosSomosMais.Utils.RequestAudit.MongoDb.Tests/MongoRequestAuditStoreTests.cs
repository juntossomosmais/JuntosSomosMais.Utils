using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

public class MongoRequestAuditStoreTests : RequestAuditMongoTestBase
{
    private static readonly Guid EntryId = Guid.Parse("0b0f4f3e-6d55-4d0a-9d0b-2f8f0c3d9a11");
    private static readonly DateTime CreatedAt = new(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAsync_EntryWithEveryField_StoresOneDocumentWithAllOfThem()
    {
        // Arrange
        var store = new MongoRequestAuditStore(Database, new MongoRequestAuditOptions());
        var entry = new RequestAuditEntry
        {
            Id = EntryId,
            CreatedAtUtc = CreatedAt,
            Method = "POST",
            Url = "https://api.example.com/v2/webhooks/order",
            QueryParams = new Dictionary<string, string> { ["filter.name"] = "a", ["$top"] = "5" },
            IpAddress = "203.0.113.7",
            RequestHeaders = new Dictionary<string, string> { ["Authorization"] = "***", ["Content-Type"] = "application/json" },
            RequestBody = """{"key":"value"}""",
            RequestBodyNote = null,
            ResponseStatusCode = 422,
            ResponseHeaders = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            ResponseBody = null,
            ResponseBodyNote = "Not stored: larger than 10 bytes.",
            RequestId = "0HN7:00000001",
            TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            Caller = new Dictionary<string, string> { ["http://schemas.example.com/claims/id"] = "user-1" },
        };

        // Act
        await store.SaveAsync(entry);

        // Assert
        var stored = Assert.Single(await Collection().Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.Equal(
            new BsonDocument
            {
                { "_id", "0b0f4f3e-6d55-4d0a-9d0b-2f8f0c3d9a11" },
                { "CreatedAtUtc", CreatedAt },
                { "Method", "POST" },
                { "Url", "https://api.example.com/v2/webhooks/order" },
                { "QueryParams", Pairs(("filter.name", "a"), ("$top", "5")) },
                { "IpAddress", "203.0.113.7" },
                { "RequestHeaders", Pairs(("Authorization", "***"), ("Content-Type", "application/json")) },
                { "RequestBody", """{"key":"value"}""" },
                { "RequestBodyNote", BsonNull.Value },
                { "ResponseStatusCode", 422 },
                { "ResponseHeaders", Pairs(("Content-Type", "application/json")) },
                { "ResponseBody", BsonNull.Value },
                { "ResponseBodyNote", "Not stored: larger than 10 bytes." },
                { "RequestId", "0HN7:00000001" },
                { "TraceParent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" },
                { "Caller", Pairs(("http://schemas.example.com/claims/id", "user-1")) },
            },
            stored);
    }

    [Fact]
    public async Task SaveAsync_EntryWithOnlyTheRequiredFields_StoresNullsAndEmptyLists()
    {
        // Arrange
        var store = new MongoRequestAuditStore(Database, new MongoRequestAuditOptions { CollectionName = "customAudit" });
        var entry = new RequestAuditEntry
        {
            Id = EntryId,
            CreatedAtUtc = CreatedAt,
            Method = "GET",
            Url = "https://api.example.com/orders",
            ResponseStatusCode = 204,
            RequestId = "0HN7:00000002",
        };

        // Act
        await store.SaveAsync(entry);

        // Assert
        var stored = Assert.Single(await Collection("customAudit").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
        Assert.Equal(
            new BsonDocument
            {
                { "_id", "0b0f4f3e-6d55-4d0a-9d0b-2f8f0c3d9a11" },
                { "CreatedAtUtc", CreatedAt },
                { "Method", "GET" },
                { "Url", "https://api.example.com/orders" },
                { "QueryParams", new BsonArray() },
                { "IpAddress", BsonNull.Value },
                { "RequestHeaders", new BsonArray() },
                { "RequestBody", BsonNull.Value },
                { "RequestBodyNote", BsonNull.Value },
                { "ResponseStatusCode", 204 },
                { "ResponseHeaders", new BsonArray() },
                { "ResponseBody", BsonNull.Value },
                { "ResponseBodyNote", BsonNull.Value },
                { "RequestId", "0HN7:00000002" },
                { "TraceParent", BsonNull.Value },
                { "Caller", new BsonArray() },
            },
            stored);
    }

    [Fact]
    public async Task SaveAsync_NullEntry_Throws()
    {
        // Arrange
        var store = new MongoRequestAuditStore(Database, new MongoRequestAuditOptions());

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!));

        // Assert
        Assert.Equal("entry", exception.ParamName);
    }

    private static BsonArray Pairs(params (string Name, string Value)[] pairs) =>
        new(pairs.Select(pair => new BsonDocument { { "Name", pair.Name }, { "Value", pair.Value } }));
}
