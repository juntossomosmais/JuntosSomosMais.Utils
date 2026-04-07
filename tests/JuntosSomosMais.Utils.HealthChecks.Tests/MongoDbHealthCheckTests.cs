using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class MongoDbHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when connection string is null")]
    public void Constructor_NullConnectionString_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbHealthCheck((string)null!));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is empty")]
    public void Constructor_EmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MongoDbHealthCheck(""));
    }

    [Fact(DisplayName = "Should throw ArgumentException when connection string is whitespace")]
    public void Constructor_WhitespaceConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MongoDbHealthCheck("   "));
    }

    [Fact(DisplayName = "Should throw ArgumentNullException when client is null")]
    public void Constructor_NullClient_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbHealthCheck((IMongoClient)null!));
    }

    [Fact(DisplayName = "Should return failure status when client throws")]
    public async Task CheckHealthAsync_ClientThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockClient = new Mock<IMongoClient>();
        mockClient.Setup(c => c.ListDatabaseNamesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused"));

        var healthCheck = new MongoDbHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("mongodb", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return healthy when server is reachable")]
    public async Task CheckHealthAsync_ServerReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockCursor = new Mock<IAsyncCursor<string>>();
        mockCursor.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var mockClient = new Mock<IMongoClient>();
        mockClient.Setup(c => c.ListDatabaseNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);

        var healthCheck = new MongoDbHealthCheck(mockClient.Object);
        var registration = new HealthCheckRegistration("mongodb", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact(DisplayName = "Should return failure status when database ping throws")]
    public async Task CheckHealthAsync_DatabaseThrows_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase.Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database unavailable"));

        var mockClient = new Mock<IMongoClient>();
        mockClient.Setup(c => c.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()))
            .Returns(mockDatabase.Object);

        var healthCheck = new MongoDbHealthCheck(mockClient.Object, "testdb");
        var registration = new HealthCheckRegistration("mongodb", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return healthy when database is reachable")]
    public async Task CheckHealthAsync_DatabaseReachable_ShouldReturnHealthy()
    {
        // Arrange
        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase.Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument("ok", 1));

        var mockClient = new Mock<IMongoClient>();
        mockClient.Setup(c => c.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()))
            .Returns(mockDatabase.Object);

        var healthCheck = new MongoDbHealthCheck(mockClient.Object, "testdb");
        var registration = new HealthCheckRegistration("mongodb", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
