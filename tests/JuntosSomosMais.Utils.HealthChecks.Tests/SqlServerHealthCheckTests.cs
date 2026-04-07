using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class SqlServerHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when connection string is null")]
    public void Constructor_NullConnectionString_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerHealthCheck(null!));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is empty")]
    public void Constructor_EmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SqlServerHealthCheck(""));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is whitespace")]
    public void Constructor_WhitespaceConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SqlServerHealthCheck("   "));
    }

    [Fact(DisplayName = "Should return failure status when server is unreachable")]
    public async Task CheckHealthAsync_UnreachableServer_ShouldReturnFailureStatus()
    {
        // Arrange
        var healthCheck = new SqlServerHealthCheck("Data Source=localhost,1;Initial Catalog=fake;Connection Timeout=1");
        var registration = new HealthCheckRegistration(
            "sqlserver",
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
