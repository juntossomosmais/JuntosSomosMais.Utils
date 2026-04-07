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
        _client = new MongoClient(connectionString);
        _databaseName = databaseName;
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
