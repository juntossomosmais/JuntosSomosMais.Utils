using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public class AzureBlobStorageHealthCheck : IHealthCheck
{
    private readonly BlobServiceClient _client;
    private readonly string? _containerName;

    public AzureBlobStorageHealthCheck(string connectionString, string? containerName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _client = new BlobServiceClient(connectionString);
        _containerName = containerName;
    }

    internal AzureBlobStorageHealthCheck(BlobServiceClient client, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _containerName = containerName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_containerName))
            {
                var containerClient = _client.GetBlobContainerClient(_containerName);
                await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            }
            else
            {
                await _client.GetPropertiesAsync(cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
