using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

/// <summary>
/// Real-Mongo base for the integration tests. Every instance (one per test) owns a unique database that is dropped
/// on dispose, so classes can run in parallel and never touch other databases. Needs a replica set (change streams).
/// The connection string comes from <c>ConnectionStrings__ConnectionString</c> (set by docker compose; CI uses the
/// localhost default). A reachable Mongo is mandatory: the tests fail loudly when it is not.
/// </summary>
public abstract class MongoTestBase : IDisposable
{
    private const string DefaultConnectionString = "mongodb://localhost:27017/?directConnection=true";

    private readonly MongoClientSettings _settings;

    protected MongoTestBase()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ConnectionString");
        var settings = MongoClientSettings.FromConnectionString(
            string.IsNullOrWhiteSpace(connectionString) ? DefaultConnectionString : connectionString);
#pragma warning disable CS0618 // GuidRepresentation: Guid ids must round-trip as UUID subtype 4
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);

        _settings = settings;
        Client = new MongoClient(settings);
        DatabaseName = "history_test_" + Guid.NewGuid().ToString("N");
        Database = Client.GetDatabase(DatabaseName);

        try
        {
            Database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "MongoDB is not reachable. The SimpleHistory.MongoDb integration tests need a replica set " +
                "(set ConnectionStrings__ConnectionString or run them through docker compose): " + ex.Message, ex);
        }
    }

    protected IMongoClient Client { get; }
    protected IMongoDatabase Database { get; }
    protected string DatabaseName { get; }

    /// <summary>The test database seen through a new client that reports every command it starts.</summary>
    protected IMongoDatabase ObservedDatabase(Action<CommandStartedEvent> onCommandStarted)
    {
        var settings = _settings.Clone();
        settings.ClusterConfigurator = cluster => cluster.Subscribe(onCommandStarted);
        return new MongoClient(settings).GetDatabase(DatabaseName);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            Client.DropDatabase(DatabaseName);
    }
}
