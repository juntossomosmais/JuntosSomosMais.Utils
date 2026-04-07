using Azure;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class AzureServiceBusHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when connection string is null")]
    public void Constructor_NullConnectionString_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureServiceBusHealthCheck((string)null!));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is empty")]
    public void Constructor_EmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AzureServiceBusHealthCheck(""));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is whitespace")]
    public void Constructor_WhitespaceConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AzureServiceBusHealthCheck("   "));
    }

    [Fact(DisplayName = "Should throw ArgumentNullException when admin client is null")]
    public void Constructor_NullAdminClient_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureServiceBusHealthCheck((ServiceBusAdministrationClient)null!));
    }

    [Fact(DisplayName = "Should throw ArgumentException when both queueName and topicName are provided")]
    public void Constructor_BothQueueAndTopic_ShouldThrowArgumentException()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AzureServiceBusHealthCheck(mockClient.Object, queueName: "q", topicName: "t"));
    }

    [Fact(DisplayName = "Should return failure status when queue check throws")]
    public async Task CheckHealthAsync_QueueThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetQueueRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object, queueName: "test-queue");
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return failure status when topic check throws")]
    public async Task CheckHealthAsync_TopicThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetTopicRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object, topicName: "test-topic");
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return failure status when namespace check throws")]
    public async Task CheckHealthAsync_NamespaceThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetNamespacePropertiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return healthy when queue is reachable")]
    public async Task CheckHealthAsync_QueueReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetQueueRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<QueueRuntimeProperties>)null!);

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object, queueName: "test-queue");
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(DisplayName = "Should return healthy when topic is reachable")]
    public async Task CheckHealthAsync_TopicReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetTopicRuntimePropertiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<TopicRuntimeProperties>)null!);

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object, topicName: "test-topic");
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(DisplayName = "Should return healthy when namespace is reachable")]
    public async Task CheckHealthAsync_NamespaceReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockClient = new Mock<ServiceBusAdministrationClient>();
        mockClient.Setup(c => c.GetNamespacePropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<NamespaceProperties>)null!);

        var healthCheck = new AzureServiceBusHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("azureservicebus", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
