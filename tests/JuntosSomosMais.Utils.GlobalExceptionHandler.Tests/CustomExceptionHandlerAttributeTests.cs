using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerAttributeTests : IClassFixture<BasicWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/customer";

    public CustomExceptionHandlerAttributeTests(BasicWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Should return 404 with NOT_FOUND_ERROR type when NotFoundException is thrown")]
    public async Task TryHandleAsync_NotFoundException_ShouldReturnNotFoundWithCorrectType()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("NOT_FOUND_ERROR", content.Type);
        Assert.Equal(404, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains("Oh, sorry!", content.Error.Msg);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
    }

    [Fact(DisplayName = "Should return 400 with VALIDATION_ERRORS type when DomainException is thrown")]
    public async Task TryHandleAsync_DomainException_ShouldReturnBadRequestWithCorrectType()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/domain-error");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("VALIDATION_ERRORS", content.Type);
        Assert.Equal(400, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
    }

    [Fact(DisplayName = "Should return 503 with custom ExceptionType when CustomTypedException is thrown")]
    public async Task TryHandleAsync_CustomTypedException_ShouldReturnServiceUnavailableWithCustomType()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/custom-typed");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("MY_CUSTOM_TYPE", content.Type);
        Assert.Equal(503, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
    }

    [Fact(DisplayName = "Should return 500 with UNEXPECTED_ERROR type when unattributed exception is thrown")]
    public async Task TryHandleAsync_UnattributedException_ShouldReturnInternalServerErrorWithUnexpectedType()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("UNEXPECTED_ERROR", content.Type);
        Assert.Equal(500, content.StatusCode);
    }

    [Fact(DisplayName = "Should return 404 with NOT_FOUND_ERROR type when child exception inherits ExceptionStatusCode attribute")]
    public async Task TryHandleAsync_ChildNotFoundException_ShouldReturnNotFoundWithInheritedAttribute()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/customer/child-not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("NOT_FOUND_ERROR", content.Type);
        Assert.Equal(404, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
    }
}
