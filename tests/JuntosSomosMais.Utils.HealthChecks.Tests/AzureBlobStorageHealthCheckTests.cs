using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class AzureBlobStorageHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when connection string is null")]
    public void Constructor_NullConnectionString_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobStorageHealthCheck((string)null!));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is empty")]
    public void Constructor_EmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AzureBlobStorageHealthCheck(""));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is whitespace")]
    public void Constructor_WhitespaceConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AzureBlobStorageHealthCheck("   "));
    }

    [Fact(DisplayName = "Should throw ArgumentNullException when client is null")]
    public void Constructor_NullClient_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobStorageHealthCheck((BlobServiceClient)null!));
    }

    [Fact(DisplayName = "Should return failure status when service throws")]
    public async Task CheckHealthAsync_ServiceThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockClient = new Mock<BlobServiceClient>();
        mockClient.Setup(c => c.GetPropertiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Service unavailable"));

        var healthCheck = new AzureBlobStorageHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("azureblobstorage", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return failure status when container check throws")]
    public async Task CheckHealthAsync_ContainerThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockContainerClient = new Mock<BlobContainerClient>();
        mockContainerClient.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Container not found"));

        var mockClient = new Mock<BlobServiceClient>();
        mockClient.Setup(c => c.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        var healthCheck = new AzureBlobStorageHealthCheck(mockClient.Object, "missing-container");
        var registration = new HealthCheckRegistration("azureblobstorage", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return healthy when service is reachable")]
    public async Task CheckHealthAsync_ServiceReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockClient = new Mock<BlobServiceClient>();
        mockClient.Setup(c => c.GetPropertiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Response<BlobServiceProperties>>(null!));

        var healthCheck = new AzureBlobStorageHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("azureblobstorage", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(DisplayName = "Should return healthy when container is reachable")]
    public async Task CheckHealthAsync_ContainerReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockContainerClient = new Mock<BlobContainerClient>();
        mockContainerClient.Setup(c => c.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Response<BlobContainerProperties>>(null!));

        var mockClient = new Mock<BlobServiceClient>();
        mockClient.Setup(c => c.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);

        var healthCheck = new AzureBlobStorageHealthCheck(mockClient.Object, "test-container");
        var registration = new HealthCheckRegistration("azureblobstorage", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
