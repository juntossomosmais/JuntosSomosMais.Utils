using System.Net;
using System.Text.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerWebAppCustomResponseTests : IClassFixture<CustomResponseWebAppFactory>
{
    private readonly HttpClient _client;

    public CustomExceptionHandlerWebAppCustomResponseTests(CustomResponseWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Should return custom response body from CustomizeResponse delegate")]
    public async Task GetAsync_ThrowException_ShouldReturnCustomResponseBody()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("trace_id", out var traceId));
        Assert.NotNull(traceId.GetString());
        Assert.NotEmpty(traceId.GetString()!);
        Assert.True(root.TryGetProperty("error_message", out var errorMessage));
        Assert.Equal("Custom exception message", errorMessage.GetString());
    }

    [Fact(DisplayName = "Should serialize response using custom JsonSerializerOptions with snake_case")]
    public async Task GetAsync_ThrowException_ShouldUseSnakeCaseNamingPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("trace_id", json);
        Assert.Contains("error_message", json);
        Assert.DoesNotContain("traceId", json);
        Assert.DoesNotContain("errorMessage", json);
    }

    [Fact(DisplayName = "Should pass resolved StatusCode and ExceptionType to CustomizeResponse delegate")]
    public async Task GetAsync_ThrowException_ShouldPassResolvedContextToDelegate()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("resolved_status_code", out var statusCode));
        Assert.Equal(500, statusCode.GetInt32());
        Assert.True(root.TryGetProperty("resolved_exception_type", out var exceptionType));
        Assert.Equal("UNEXPECTED_ERROR", exceptionType.GetString());
    }

    [Fact(DisplayName = "Should pass resolved 404 StatusCode and NOT_FOUND_ERROR ExceptionType for attributed exception")]
    public async Task GetAsync_ThrowAttributedException_ShouldPassResolvedAttributeContextToDelegate()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("resolved_status_code", out var statusCode));
        Assert.Equal(404, statusCode.GetInt32());
        Assert.True(root.TryGetProperty("resolved_exception_type", out var exceptionType));
        Assert.Equal("NOT_FOUND_ERROR", exceptionType.GetString());
    }
}
