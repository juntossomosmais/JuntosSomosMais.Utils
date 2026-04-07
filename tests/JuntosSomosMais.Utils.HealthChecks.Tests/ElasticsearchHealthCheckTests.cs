using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Moq.Protected;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class ElasticsearchHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when URI is null")]
    public void Constructor_NullUri_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ElasticsearchHealthCheck((Uri)null!));
    }

    [Fact(DisplayName = "Should return failure status when server is unreachable")]
    public async Task CheckHealthAsync_UnreachableServer_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object);
        var healthCheck = new ElasticsearchHealthCheck(httpClient, new Uri("http://localhost:9200"));
        var registration = new HealthCheckRegistration("elasticsearch", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact(DisplayName = "Should return failure status when server returns error")]
    public async Task CheckHealthAsync_ServerReturnsError_ShouldReturnFailureStatus()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var httpClient = new HttpClient(mockHandler.Object);
        var healthCheck = new ElasticsearchHealthCheck(httpClient, new Uri("http://localhost:9200"));
        var registration = new HealthCheckRegistration("elasticsearch", healthCheck, HealthStatus.Unhealthy, null);
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
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(mockHandler.Object);
        var healthCheck = new ElasticsearchHealthCheck(httpClient, new Uri("http://localhost:9200"));
        var registration = new HealthCheckRegistration("elasticsearch", healthCheck, HealthStatus.Unhealthy, null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
