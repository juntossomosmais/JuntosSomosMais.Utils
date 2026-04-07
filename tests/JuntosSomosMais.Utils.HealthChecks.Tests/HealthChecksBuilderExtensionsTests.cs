using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class HealthChecksBuilderExtensionsTests
{
    [Fact(DisplayName = "Should register SqlServerHealthCheck with default name")]
    public void AddSqlServerHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddSqlServerHealthCheck("Data Source=localhost;Initial Catalog=test");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "sqlserver");
    }

    [Fact(DisplayName = "Should register SqlServerHealthCheck with custom name and tags")]
    public void AddSqlServerHealthCheck_CustomNameAndTags_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddSqlServerHealthCheck("Data Source=localhost;Initial Catalog=test", name: "db", tags: ["crucial"]);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Assert.Single(options.Registrations, r => r.Name == "db");
        Assert.Contains("crucial", registration.Tags);
    }

    [Fact(DisplayName = "Should register RedisHealthCheck with default name")]
    public void AddRedisHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddRedisHealthCheck(new Uri("redis://localhost:6379"));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "redis");
    }

    [Fact(DisplayName = "Should register RabbitMQHealthCheck with default name")]
    public void AddRabbitMQHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddRabbitMQHealthCheck(new Uri("amqp://localhost:5672"));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "rabbitmq");
    }

    [Fact(DisplayName = "Should register HangfireHealthCheck with default name")]
    public void AddHangfireHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<JobStorage>().Object);
        var builder = services.AddHealthChecks();

        // Act
        builder.AddHangfireHealthCheck();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "hangfire");
    }

    [Fact(DisplayName = "Should resolve HangfireHealthCheck from DI using JobStorage")]
    public void AddHangfireHealthCheck_ResolvesFromDI_ShouldCreateInstanceWithJobStorage()
    {
        // Arrange
        var mockStorage = new Mock<JobStorage>();
        var services = new ServiceCollection();
        services.AddSingleton(mockStorage.Object);
        services.AddHealthChecks().AddHangfireHealthCheck();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Assert.Single(options.Registrations, r => r.Name == "hangfire");

        // Act
        var instance = registration.Factory(provider);

        // Assert
        Assert.IsType<HangfireHealthCheck>(instance);
    }

    [Fact(DisplayName = "Should support fluent chaining of multiple health checks")]
    public void FluentChaining_MultipleChecks_ShouldRegisterAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<JobStorage>().Object);

        // Act
        services.AddHealthChecks()
            .AddSqlServerHealthCheck("Data Source=localhost;Initial Catalog=test", tags: ["crucial"])
            .AddRedisHealthCheck(new Uri("redis://localhost:6379"))
            .AddRabbitMQHealthCheck(new Uri("amqp://localhost:5672"))
            .AddHangfireHealthCheck();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Equal(4, options.Registrations.Count);
    }
}
