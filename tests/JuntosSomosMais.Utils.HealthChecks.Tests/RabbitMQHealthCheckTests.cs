using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class RabbitMQHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when URI is null")]
    public void Constructor_NullUri_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMQHealthCheck(null!));
    }

    [Fact(DisplayName = "Should return failure status when broker is unreachable")]
    public async Task CheckHealthAsync_UnreachableBroker_ShouldReturnFailureStatus()
    {
        // Arrange
        var healthCheck = new RabbitMQHealthCheck(new Uri("amqp://localhost:1"));
        var registration = new HealthCheckRegistration(
            "rabbitmq",
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
}
