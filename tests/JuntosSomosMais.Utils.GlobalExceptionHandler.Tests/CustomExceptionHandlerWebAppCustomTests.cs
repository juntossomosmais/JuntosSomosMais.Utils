using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Web;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerWebAppCustomTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/product";
    private const string ValidationErrors = "VALIDATION_ERRORS";

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
        var response = await _client.GetAsync($"{BaseUrl}?count={count}");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
        content.Should().NotBeNullOrEmpty().And.HaveCount(count);
    }

    [Fact(DisplayName = "Should return bad request with stack trace when ViewStackTrace is true")]
    public async Task GetAsync_ThrowDomainException_ShouldReturnBadRequestWithDetail()
    {
        var response = await _client.GetAsync($"{BaseUrl}/domain");
        response.Should().Be400BadRequest();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        content!.Type.Should().Be(ValidationErrors);
        content.Error!.Msg.Should().Be("Custom domain exception message");
        content.Error.Detail.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from domain url when returnProduct is true")]
    public async Task GetAsyncDomain_GetProducts_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/domain?returnProduct=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return bad request with custom exception type")]
    public async Task GetAsync_ThrowCustomDomainException_ShouldReturnBadRequestWithCustomType()
    {
        var response = await _client.GetAsync($"{BaseUrl}/custom-domain");
        response.Should().Be400BadRequest();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        content!.Type.Should().Be("OTHER_CUSTOM_TYPE");
        content.Error!.Msg.Should().Be("Custom domain exception message");
        content.Error.Detail.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from custom-domain url when returnProduct is true")]
    public async Task GetAsyncCustomDomain_GetProducts_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/custom-domain?returnProduct=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return 500 and bypass handler when method has IgnoreCustomException attribute")]
    public async Task GetAsync_IgnoreExceptionMethodAttribute_ShouldReturnInternalServerError()
    {
        var response = await _client.GetAsync($"{BaseUrl}/ignore");
        response.Should().Be500InternalServerError();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Some error ignore method");
    }

    [Fact(DisplayName = "Should return 500 and bypass handler when class has IgnoreCustomException attribute")]
    public async Task GetAsync_IgnoreExceptionClassAttribute_ShouldReturnInternalServerError()
    {
        var response = await _client.GetAsync("/values");
        response.Should().Be500InternalServerError();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Some error ignore class");
    }

    [Fact(DisplayName = "Should return 409 Conflict for custom exception mapping")]
    public async Task GetAsync_ThrowConflictException_ShouldReturnConflict()
    {
        var response = await _client.GetAsync($"{BaseUrl}/conflict");
        response.Should().HaveStatusCode(System.Net.HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        content!.Error!.Msg.Should().Be("Conflict error message");
    }

    [Fact(DisplayName = "Should use base-class ExceptionMappings assignment for a subclass exception")]
    public async Task GetAsync_ThrowConcreteSubException_ShouldReturnMappedStatusCodeFromBaseClass()
    {
        var response = await _client.GetAsync($"{BaseUrl}/base-class-mapping");
        response.Should().HaveStatusCode(System.Net.HttpStatusCode.UnprocessableEntity);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorDetailResponse>();
        content!.Error!.Msg.Should().Be("Base class mapping error");
    }
}
