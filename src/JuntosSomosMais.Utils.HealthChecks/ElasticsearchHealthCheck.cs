using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly Uri _uri;

    public ElasticsearchHealthCheck(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _uri = uri;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    internal ElasticsearchHealthCheck(HttpClient httpClient, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(uri);
        _httpClient = httpClient;
        _uri = uri;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(_uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
