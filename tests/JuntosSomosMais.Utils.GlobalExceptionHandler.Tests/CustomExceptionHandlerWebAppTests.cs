using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Web;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerWebAppTests : IClassFixture<BasicWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/customer";
    private const string UnexpectedError = "UNEXPECTED_ERROR";
    private const string ValidationErrors = "VALIDATION_ERRORS";

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
        var response = await _client.GetAsync($"{BaseUrl}?count={count}");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty().And.HaveCount(count);
    }

    [Fact(DisplayName = "Should return bad request and throw a domain exception")]
    public async Task GetAsync_ThrowDomainException_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync($"{BaseUrl}/domain");
        response.Should().Be400BadRequest();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        content!.Error!.Msg.Should().Be("Custom domain exception message");
        content.Type.Should().Be(ValidationErrors);
    }

    [Fact(DisplayName = "Should return unauthorized and throw an unauthorized exception")]
    public async Task GetAsync_ThrowUnauthorizedException_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync($"{BaseUrl}/unauthorized");
        response.Should().Be401Unauthorized();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        content!.Error!.Msg.Should().Be("Custom unauthorized exception message");
        content.Type.Should().Be(ValidationErrors);
    }

    [Fact(DisplayName = "Should return forbidden and throw a cannot access exception")]
    public async Task GetAsync_ThrowCannotAccessException_ShouldReturnForbidden()
    {
        var response = await _client.GetAsync($"{BaseUrl}/cannot-access");
        response.Should().Be403Forbidden();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        content!.Error!.Msg.Should().Be("Custom cannot access exception message");
        content.Type.Should().Be(ValidationErrors);
    }

    [Fact(DisplayName = "Should return not found and throw a not found exception")]
    public async Task GetAsync_ThrowNotFoundException_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"{BaseUrl}/not-found");
        response.Should().Be404NotFound();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        content!.Error!.Msg.Should().Be("Custom not found exception message");
        content.Type.Should().Be(ValidationErrors);
    }

    [Fact(DisplayName = "Should return internal server error and throw a plain exception")]
    public async Task GetAsync_ThrowException_ShouldReturnInternalServerError()
    {
        var response = await _client.GetAsync($"{BaseUrl}/exception");
        response.Should().Be500InternalServerError();
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        content!.Error!.Msg.Should().Be("Custom exception message");
        content.Type.Should().Be(UnexpectedError);
    }

    [Fact(DisplayName = "Should return Ok from domain url when returnCustomer is true")]
    public async Task GetAsyncDomain_GetCustomers_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/domain?returnCustomer=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from unauthorized url when returnCustomer is true")]
    public async Task GetAsyncUnauthorized_GetCustomers_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/unauthorized?returnCustomer=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from cannot-access url when returnCustomer is true")]
    public async Task GetAsyncCannotAccess_GetCustomers_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/cannot-access?returnCustomer=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from not-found url when returnCustomer is true")]
    public async Task GetAsyncNotFound_GetCustomers_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/not-found?returnCustomer=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Should return Ok from exception url when returnCustomer is true")]
    public async Task GetAsyncException_GetCustomers_ShouldReturnOk()
    {
        var response = await _client.GetAsync($"{BaseUrl}/exception?returnCustomer=true");
        response.Should().Be200Ok();
        var content = await response.Content.ReadFromJsonAsync<IEnumerable<Customer>>();
        content.Should().NotBeNullOrEmpty();
    }
}
