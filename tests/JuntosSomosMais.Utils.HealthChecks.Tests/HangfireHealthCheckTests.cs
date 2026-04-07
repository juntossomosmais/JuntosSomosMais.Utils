using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class HangfireHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when storage is null")]
    public void Constructor_NullStorage_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HangfireHealthCheck(null!));
    }

    [Fact(DisplayName = "Should return failure status when storage throws")]
    public async Task CheckHealthAsync_StorageThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockStorage = new Mock<JobStorage>();
        mockStorage.Setup(s => s.GetMonitoringApi())
            .Throws(new Exception("Storage unavailable"));

        var healthCheck = new HangfireHealthCheck(mockStorage.Object);
        var registration = new HealthCheckRegistration(
            "hangfire",
            healthCheck,
            HealthStatus.Unhealthy,
            null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return healthy when storage is reachable")]
    public async Task CheckHealthAsync_StorageReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockMonitoringApi = new Mock<IMonitoringApi>();
        mockMonitoringApi.Setup(m => m.FailedCount()).Returns(0);

        var mockStorage = new Mock<JobStorage>();
        mockStorage.Setup(s => s.GetMonitoringApi())
            .Returns(mockMonitoringApi.Object);

        var healthCheck = new HangfireHealthCheck(mockStorage.Object);
        var registration = new HealthCheckRegistration(
            "hangfire",
            healthCheck,
            HealthStatus.Unhealthy,
            null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
