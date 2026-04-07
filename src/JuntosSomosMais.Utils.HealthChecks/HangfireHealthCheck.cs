using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public class HangfireHealthCheck : IHealthCheck
{
    private readonly JobStorage? _storage;

    public HangfireHealthCheck() { }

    internal HangfireHealthCheck(JobStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _storage = storage;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                var api = (_storage ?? JobStorage.Current).GetMonitoringApi();
                api.FailedCount();
            }, cancellationToken).WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
