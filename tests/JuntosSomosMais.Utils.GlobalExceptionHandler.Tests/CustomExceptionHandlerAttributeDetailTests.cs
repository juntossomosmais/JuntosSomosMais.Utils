using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerAttributeDetailTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/product";

    public CustomExceptionHandlerAttributeDetailTests(CustomWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Should return 404 with detail when NotFoundException is thrown and ViewStackTrace is true")]
    public async Task TryHandleAsync_NotFoundExceptionWithViewStackTrace_ShouldReturnNotFoundWithDetail()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        Assert.NotNull(content);
        Assert.Equal("NOT_FOUND_ERROR", content.Type);
        Assert.Equal(404, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
        Assert.Contains("NotFoundException", content.Error.Detail);
    }

    [Fact(DisplayName = "Should return 400 with detail when DomainException is thrown and ViewStackTrace is true")]
    public async Task TryHandleAsync_DomainExceptionWithViewStackTrace_ShouldReturnBadRequestWithDetail()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/domain-error");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        Assert.NotNull(content);
        Assert.Equal("VALIDATION_ERRORS", content.Type);
        Assert.Equal(400, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.Contains("DomainException", content.Error.Detail);
    }
}
