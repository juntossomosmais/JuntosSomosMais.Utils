using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace JuntosSomosMais.Utils.HealthChecks;

public class RabbitMQHealthCheck : IHealthCheck
{
    private readonly Uri _uri;

    public RabbitMQHealthCheck(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _uri = uri;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = _uri };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
