using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.HealthChecks;

public class MongoDbHealthCheck : IHealthCheck
{
    private static readonly Lazy<BsonDocumentCommand<BsonDocument>> PingCommand =
        new(() => new(BsonDocument.Parse("{ping:1}")));

    private readonly IMongoClient _client;
    private readonly string? _databaseName;

    public MongoDbHealthCheck(string connectionString, string? databaseName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        EnsureSupportedDriver(typeof(MongoClient).Assembly.GetName().Version);
        _client = new MongoClient(connectionString);
        _databaseName = databaseName;
    }

    // Compiled against 2.x, which looks for the cursor and read preference types in MongoDB.Driver.Core. 3.0 moved
    // them into MongoDB.Driver, so on 3.x CheckHealthAsync throws before reaching its catch. MongoClient is in
    // MongoDB.Driver in both lines, so this guard still runs on 3.x.
    internal static void EnsureSupportedDriver(Version? driverVersion)
    {
        if (driverVersion is { Major: >= 3 })
            throw new NotSupportedException(
                $"MongoDbHealthCheck requires MongoDB.Driver 2.x, but MongoDB.Driver {driverVersion} was loaded.");
    }

    internal MongoDbHealthCheck(IMongoClient client, string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _databaseName = databaseName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_databaseName))
            {
                await _client.GetDatabase(_databaseName)
                    .RunCommandAsync(PingCommand.Value, cancellationToken: cancellationToken);
            }
            else
            {
                using var cursor = await _client.ListDatabaseNamesAsync(cancellationToken);
                await cursor.MoveNextAsync(cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
