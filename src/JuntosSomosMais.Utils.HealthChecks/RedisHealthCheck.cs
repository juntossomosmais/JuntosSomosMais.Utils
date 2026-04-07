using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace JuntosSomosMais.Utils.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly Uri _uri;

    public RedisHealthCheck(Uri uri)
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
            var options = BuildConfigurationOptions(_uri);
            using var connection = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(cancellationToken);
            var db = connection.GetDatabase();
            await db.PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }

    internal static ConfigurationOptions BuildConfigurationOptions(Uri uri)
    {
        var options = new ConfigurationOptions
        {
            ConnectTimeout = 5000,
            AbortOnConnectFail = true
        };
        options.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':');
            if (parts.Length == 2)
            {
                options.User = Uri.UnescapeDataString(parts[0]);
                options.Password = Uri.UnescapeDataString(parts[1]);
            }
            else
            {
                options.Password = Uri.UnescapeDataString(parts[0]);
            }
        }

        if (uri.AbsolutePath.Length > 1)
        {
            var dbSegment = uri.AbsolutePath.TrimStart('/');
            if (!int.TryParse(dbSegment, out var database))
                throw new ArgumentException($"Invalid Redis database number: '{dbSegment}'");
            options.DefaultDatabase = database;
        }

        return options;
    }
}
