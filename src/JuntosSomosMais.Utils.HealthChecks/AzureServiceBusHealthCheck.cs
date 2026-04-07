using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public class AzureServiceBusHealthCheck : IHealthCheck
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly string? _queueName;
    private readonly string? _topicName;

    public AzureServiceBusHealthCheck(string connectionString, string? queueName = null, string? topicName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (!string.IsNullOrEmpty(queueName) && !string.IsNullOrEmpty(topicName))
            throw new ArgumentException("Cannot specify both queueName and topicName. Use separate health checks for each.");
        _adminClient = new ServiceBusAdministrationClient(connectionString);
        _queueName = queueName;
        _topicName = topicName;
    }

    internal AzureServiceBusHealthCheck(ServiceBusAdministrationClient adminClient, string? queueName = null, string? topicName = null)
    {
        ArgumentNullException.ThrowIfNull(adminClient);
        if (!string.IsNullOrEmpty(queueName) && !string.IsNullOrEmpty(topicName))
            throw new ArgumentException("Cannot specify both queueName and topicName. Use separate health checks for each.");
        _adminClient = adminClient;
        _queueName = queueName;
        _topicName = topicName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_queueName))
            {
                await _adminClient.GetQueueRuntimePropertiesAsync(_queueName, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(_topicName))
            {
                await _adminClient.GetTopicRuntimePropertiesAsync(_topicName, cancellationToken);
            }
            else
            {
                await _adminClient.GetNamespacePropertiesAsync(cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
