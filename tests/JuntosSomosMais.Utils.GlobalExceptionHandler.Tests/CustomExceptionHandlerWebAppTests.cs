using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerWebAppTests : IClassFixture<BasicWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/customer";

    public CustomExceptionHandlerWebAppTests(BasicWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory(DisplayName = "Should return Ok and get customers")]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task GetAsync_GetCustomers_ShouldReturnOk(int count)
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}?count={count}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        Assert.NotNull(content);
        Assert.Equal(count, content.Count());
    }

    [Fact(DisplayName = "Should return 500 with friendly message and requestId when exception is thrown")]
    public async Task GetAsync_ThrowException_ShouldReturnInternalServerError()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("UNEXPECTED_ERROR", content.Type);
        Assert.Equal(500, content.StatusCode);
        Assert.NotNull(content.Error);
        Assert.NotNull(content.Error.RequestId);
        Assert.NotEmpty(content.Error.RequestId);
        Assert.Contains("Oh, sorry!", content.Error.Msg);
        Assert.Contains(content.Error.RequestId, content.Error.Msg);
    }

    [Fact(DisplayName = "Should return Ok from exception url when returnCustomer is true")]
    public async Task GetAsyncException_GetCustomers_ShouldReturnOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"{BaseUrl}/exception?returnCustomer=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }
}
