using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerLogLevelTests : IClassFixture<LoggingWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly FakeLogCollector _logCollector;
    private const string HandlerCategory = "JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptionHandler";

    public CustomExceptionHandlerLogLevelTests(LoggingWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _logCollector = factory.LogCollector;
        _logCollector.Clear();
    }

    [Fact(DisplayName = "Should log at Debug level when ValidationException is thrown")]
    public async Task TryHandleAsync_ValidationException_ShouldLogDebug()
    {
        // Arrange
        var request = new CreatePersonRequest { Name = "John", Email = "not-an-email" };

        // Act
        var response = await _client.PostAsJsonAsync("/person/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("validation exception"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Debug level when attributed 404 exception is thrown")]
    public async Task TryHandleAsync_NotFoundException_ShouldLogDebug()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Customer not found"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Debug level when attributed 400 exception is thrown")]
    public async Task TryHandleAsync_DomainException_ShouldLogDebug()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/domain-error");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Invalid customer data"));
        Assert.Equal(LogLevel.Debug, entry.Level);
    }

    [Fact(DisplayName = "Should log at Error level when unattributed 500 exception is thrown")]
    public async Task TryHandleAsync_UnattributedException_ShouldLogError()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Custom exception message"));
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact(DisplayName = "Should log at Error level when attributed 503 exception is thrown")]
    public async Task TryHandleAsync_ServiceUnavailableException_ShouldLogError()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/custom-typed");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var entry = Assert.Single(_logCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Message.Contains("Service is down"));
        Assert.Equal(LogLevel.Error, entry.Level);
    }
}
