using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb.Tests;

/// <summary>
/// Real-Mongo base. Every instance (one per test) owns a unique database that is dropped on dispose, so the tests
/// never touch other data. The connection string comes from <c>ConnectionStrings__ConnectionString</c>, with a
/// localhost default.
/// </summary>
public abstract class RequestAuditMongoTestBase : IDisposable
{
    private const string DefaultConnectionString = "mongodb://localhost:27017/?directConnection=true";

    private readonly MongoClient _client;
    private readonly string _databaseName = "request_audit_test_" + Guid.NewGuid().ToString("N");

    protected RequestAuditMongoTestBase()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString");
        var settings = MongoClientSettings.FromConnectionString(
            string.IsNullOrWhiteSpace(connectionString) ? DefaultConnectionString : connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

        _client = new MongoClient(settings);
        Database = _client.GetDatabase(_databaseName);
    }

    protected IMongoDatabase Database { get; }

    protected IMongoCollection<BsonDocument> Collection(string name = "requestAuditLogs") =>
        Database.GetCollection<BsonDocument>(name);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            _client.DropDatabase(_databaseName);
    }
}
