using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public static class HealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddSqlServerHealthCheck(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "sqlserver",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, new SqlServerHealthCheck(connectionString), failureStatus, tags ?? []);
    }

    public static IHealthChecksBuilder AddRedisHealthCheck(
        this IHealthChecksBuilder builder,
        Uri uri,
        string name = "redis",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, new RedisHealthCheck(uri), failureStatus, tags ?? []);
    }

    public static IHealthChecksBuilder AddRabbitMQHealthCheck(
        this IHealthChecksBuilder builder,
        Uri uri,
        string name = "rabbitmq",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck(name, new RabbitMQHealthCheck(uri), failureStatus, tags ?? []);
    }

    public static IHealthChecksBuilder AddHangfireHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "hangfire",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new HangfireHealthCheck(sp.GetRequiredService<JobStorage>()),
            failureStatus,
            tags ?? []));
    }
}
