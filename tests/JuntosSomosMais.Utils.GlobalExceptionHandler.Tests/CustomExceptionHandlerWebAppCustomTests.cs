using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerWebAppCustomTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/product";

    public CustomExceptionHandlerWebAppCustomTests(CustomWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory(DisplayName = "Should return Ok and get products")]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task GetAsync_GetProducts_ShouldReturnOk(int count)
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}?count={count}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
        Assert.NotNull(content);
        Assert.Equal(count, content.Count());
    }

    [Fact(DisplayName = "Should return 500 with detail when ViewStackTrace is true")]
    public async Task GetAsync_ThrowException_ShouldReturnInternalServerErrorWithDetail()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        Assert.NotNull(content);
        Assert.Equal("UNEXPECTED_ERROR", content.Type);
        Assert.Equal(500, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains("Oh, sorry!", content.Error.Msg);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
        Assert.Contains("Something went wrong", content.Error.Detail);
        Assert.Contains("InvalidOperationException", content.Error.Detail);
    }

    [Fact(DisplayName = "Should return Ok from exception url when returnProduct is true")]
    public async Task GetAsyncException_GetProducts_ShouldReturnOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/exception?returnProduct=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact(DisplayName = "Should return 500 and bypass handler when method has IgnoreCustomException attribute")]
    public async Task GetAsync_IgnoreExceptionMethodAttribute_ShouldReturnInternalServerError()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/ignore");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Some error ignore method", content);
    }

    [Fact(DisplayName = "Should return 500 and bypass handler when class has IgnoreCustomException attribute")]
    public async Task GetAsync_IgnoreExceptionClassAttribute_ShouldReturnInternalServerError()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/values");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Some error ignore class", content);
    }
}
