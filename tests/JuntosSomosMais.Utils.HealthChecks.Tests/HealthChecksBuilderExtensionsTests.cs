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

    [Fact(DisplayName = "Should register AzureBlobStorageHealthCheck with default name")]
    public void AddAzureBlobStorageHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddAzureBlobStorageHealthCheck("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "azureblobstorage");
    }

    [Fact(DisplayName = "Should register AzureServiceBusQueueHealthCheck with default name")]
    public void AddAzureServiceBusQueueHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddAzureServiceBusQueueHealthCheck("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dGVzdGtleXRlc3RrZXk=", "test-queue");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "azureservicebus-queue");
    }

    [Fact(DisplayName = "Should register AzureServiceBusTopicHealthCheck with default name")]
    public void AddAzureServiceBusTopicHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddAzureServiceBusTopicHealthCheck("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dGVzdGtleXRlc3RrZXk=", "test-topic");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "azureservicebus-topic");
    }

    [Fact(DisplayName = "Should register MongoDbHealthCheck with default name")]
    public void AddMongoDbHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddMongoDbHealthCheck("mongodb://localhost:27017");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "mongodb");
    }

    [Fact(DisplayName = "Should register ElasticsearchHealthCheck with default name")]
    public void AddElasticsearchHealthCheck_DefaultName_ShouldRegisterCheck()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddHealthChecks();

        // Act
        builder.AddElasticsearchHealthCheck(new Uri("http://localhost:9200"));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Contains(options.Registrations, r => r.Name == "elasticsearch");
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
            .AddHangfireHealthCheck()
            .AddAzureBlobStorageHealthCheck("DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;")
            .AddAzureServiceBusQueueHealthCheck("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dGVzdGtleXRlc3RrZXk=", "test-queue")
            .AddAzureServiceBusTopicHealthCheck("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=dGVzdGtleXRlc3RrZXk=", "test-topic")
            .AddMongoDbHealthCheck("mongodb://localhost:27017")
            .AddElasticsearchHealthCheck(new Uri("http://localhost:9200"));

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>().Value;
        Assert.Equal(9, options.Registrations.Count);
    }
}
