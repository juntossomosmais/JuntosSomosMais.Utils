using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerValidationTests : IClassFixture<ValidationWebAppFactory>
{
    private readonly HttpClient _client;
    private const string BaseUrl = "/person";

    public CustomExceptionHandlerValidationTests(ValidationWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Should return Ok when request is valid")]
    public async Task Create_ValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new CreatePersonRequest { Name = "John", Email = "john@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync(BaseUrl, request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CreatePersonRequest>();
        Assert.NotNull(content);
        Assert.Equal("John", content.Name);
    }

    [Fact(DisplayName = "Should return 400 with VALIDATION_ERRORS type when model state is invalid")]
    public async Task Create_EmptyBody_ShouldReturnBadRequestWithValidationErrors()
    {
        // Arrange
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(BaseUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomValidationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("VALIDATION_ERRORS", body.Type);
        Assert.Equal(400, body.StatusCode);
        Assert.NotNull(body.Error);
    }

    [Fact(DisplayName = "Should return field-level errors for missing required fields via model state")]
    public async Task Create_MissingFields_ShouldReturnFieldLevelErrors()
    {
        // Arrange
        var content = new StringContent("""{"Name": null, "Email": null}""", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(BaseUrl, content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomValidationErrorResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal("VALIDATION_ERRORS", body.Type);
        Assert.Equal(400, body.StatusCode);
        Assert.NotNull(body.Error);
        Assert.True(body.Error.ContainsKey("Name"));
        Assert.True(body.Error.ContainsKey("Email"));
    }

    [Fact(DisplayName = "Should return 400 with field errors when ValidateAndThrowAsync fails")]
    public async Task CreateWithValidateAndThrow_InvalidRequest_ShouldReturnValidationErrors()
    {
        // Arrange - values pass [Required] but fail FluentValidation rules
        var request = new CreatePersonRequest { Name = "John", Email = "not-an-email" };

        // Act
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.Equal("VALIDATION_ERRORS", typeProp.GetString());

        Assert.True(root.TryGetProperty("statusCode", out var statusCodeProp));
        Assert.Equal(400, statusCodeProp.GetInt32());

        Assert.True(root.TryGetProperty("error", out var errorProp));
        Assert.Equal(JsonValueKind.Object, errorProp.ValueKind);
        Assert.True(errorProp.TryGetProperty("Email", out var emailProp));
        Assert.Contains("The field [Email] must be a valid email address", emailProp[0].GetString());
    }

    [Fact(DisplayName = "Should return Ok when ValidateAndThrowAsync succeeds")]
    public async Task CreateWithValidateAndThrow_ValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new CreatePersonRequest { Name = "Jane", Email = "jane@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync($"{BaseUrl}/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CreatePersonRequest>();
        Assert.NotNull(content);
        Assert.Equal("Jane", content.Name);
    }

    [Fact(DisplayName = "Should return model-state error when empty body is sent to validate-and-throw endpoint")]
    public async Task CreateWithValidateAndThrow_EmptyBody_ShouldReturnModelStateErrorBeforeFluentValidation()
    {
        // Arrange
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync($"{BaseUrl}/validate-and-throw", content);

        // Assert - model-state [Required] catches it before FluentValidation runs
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.Equal("VALIDATION_ERRORS", typeProp.GetString());

        Assert.True(root.TryGetProperty("statusCode", out var statusCodeProp));
        Assert.Equal(400, statusCodeProp.GetInt32());

        Assert.True(root.TryGetProperty("error", out var errorProp));
        Assert.Equal(JsonValueKind.Object, errorProp.ValueKind);
    }

    [Fact(DisplayName = "Should return response with type field for frontend errorConverter compatibility")]
    public async Task Create_InvalidRequest_ResponseShouldHaveTypeFieldForFrontendCompatibility()
    {
        // Arrange
        var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(BaseUrl, content);

        // Assert
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("type", out var typeProp));
        Assert.Equal("VALIDATION_ERRORS", typeProp.GetString());

        Assert.True(root.TryGetProperty("statusCode", out var statusCodeProp));
        Assert.Equal(400, statusCodeProp.GetInt32());

        Assert.True(root.TryGetProperty("error", out var errorProp));
        Assert.Equal(JsonValueKind.Object, errorProp.ValueKind);
    }
}
