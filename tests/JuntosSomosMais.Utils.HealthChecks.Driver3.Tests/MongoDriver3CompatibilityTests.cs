using Hangfire;
using Hangfire.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Moq;
using RabbitMQ.Client.Exceptions;
using StackExchange.Redis;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Driver3.Tests;

/// <summary>
/// Runs the package, compiled against MongoDB.Driver 2.x, in a service that resolves MongoDB.Driver 3.x.
/// </summary>
public class MongoDriver3CompatibilityTests
{
    [Fact(DisplayName = "Should resolve MongoDB.Driver 3.x in this project")]
    public void MongoClient_ResolvedDriver_ShouldBeMajor3()
    {
        // Arrange
        // Act
        var version = typeof(MongoClient).Assembly.GetName().Version;

        // Assert
        Assert.NotNull(version);
        Assert.Equal(3, version.Major);
    }

    [Fact(DisplayName = "Should load every type of the package on MongoDB.Driver 3.x")]
    public void GetTypes_PackageAssemblyOnDriver3_ShouldNotThrow()
    {
        // Arrange
        var assembly = typeof(MongoDbHealthCheck).Assembly;

        // Act
        var exception = Record.Exception(() => assembly.GetTypes());

        // Assert
        Assert.Null(exception);
    }

    [Fact(DisplayName = "Should fail at registration when the MongoDB check is added on MongoDB.Driver 3.x")]
    public void AddMongoDbHealthCheck_Driver3_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new ServiceCollection().AddHealthChecks();

        // Act
        var exception = Assert.Throws<NotSupportedException>(
            () => builder.AddMongoDbHealthCheck("mongodb://localhost:1"));

        // Assert
        Assert.StartsWith("MongoDbHealthCheck requires MongoDB.Driver 2.x, but MongoDB.Driver 3.", exception.Message);
    }

    [Fact(DisplayName = "Should run the SQL Server, Redis, RabbitMQ and Hangfire checks on MongoDB.Driver 3.x")]
    public async Task CheckHealthAsync_OtherChecksOnDriver3_ShouldRunTheirOwnLogicAsync()
    {
        // Arrange
        var monitoringApi = new Mock<IMonitoringApi>();
        monitoringApi.Setup(m => m.FailedCount()).Returns(0);
        var storage = new Mock<JobStorage>();
        storage.Setup(s => s.GetMonitoringApi()).Returns(monitoringApi.Object);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(storage.Object);
        services.AddHealthChecks()
            .AddSqlServerHealthCheck("Data Source=localhost,1;Initial Catalog=fake;Connection Timeout=1")
            .AddRedisHealthCheck(new Uri("redis://localhost:1/0"))
            .AddRabbitMQHealthCheck(new Uri("amqp://localhost:1"))
            .AddHangfireHealthCheck();
        await using var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        var report = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, report.Entries["hangfire"].Status);
        monitoringApi.Verify(m => m.FailedCount(), Times.Once());
        // Nothing listens on port 1, so each check reports its own connection failure.
        Assert.IsType<SqlException>(report.Entries["sqlserver"].Exception);
        Assert.IsType<RedisConnectionException>(report.Entries["redis"].Exception);
        Assert.IsType<BrokerUnreachableException>(report.Entries["rabbitmq"].Exception);
    }
}
