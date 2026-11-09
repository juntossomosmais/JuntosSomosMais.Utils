using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

/// <summary>
/// Discriminating tests for <see cref="IgnoreCustomExceptionAttribute"/>: <c>ViewStackTrace</c> is off, so the
/// exception message reaches the body only when the fallback writes it, never when the handler does.
/// </summary>
public class CustomExceptionHandlerIgnoreAttributeTests
{
    private const string HandlerCategory = "JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptionHandler";

    public static TheoryData<ExceptionPipeline, string, string> IgnoredEndpoints()
    {
        var data = new TheoryData<ExceptionPipeline, string, string>();
        foreach (var pipeline in Enum.GetValues<ExceptionPipeline>())
        {
            data.Add(pipeline, "/values", "Some error ignore class");
            data.Add(pipeline, "/product/ignore", "Some error ignore method");
            data.Add(pipeline, ExceptionPipelineHost.MinimalIgnorePath, ExceptionPipelineHost.MinimalIgnoreMessage);
        }

        return data;
    }

    public static TheoryData<ExceptionPipeline> Pipelines()
    {
        var data = new TheoryData<ExceptionPipeline>();
        foreach (var pipeline in Enum.GetValues<ExceptionPipeline>())
            data.Add(pipeline);
        return data;
    }

    // Framework behaviour the handler must cope with: the middleware clears the endpoint before calling any
    // IExceptionHandler and keeps it only in IExceptionHandlerFeature.Endpoint.
    [Theory(DisplayName = "Should give the exception handlers no endpoint, only the feature endpoint with the attribute")]
    [MemberData(nameof(IgnoredEndpoints))]
    public async Task TryHandleAsync_EndpointWithIgnoreAttribute_ShouldSeeEndpointOnlyThroughFeature(
        ExceptionPipeline pipeline, string path, string exceptionMessage)
    {
        // Arrange
        await using var host = await ExceptionPipelineHost.StartAsync(pipeline);

        // Act
        await host.Client.GetAsync(path);

        // Assert
        var entry = Assert.Single(host.Probe.Entries);
        Assert.Equal(path, entry.Path);
        Assert.False(entry.HasEndpointFromContext);
        Assert.True(entry.HasAttributeOnFeatureEndpoint, $"feature endpoint without the attribute for '{exceptionMessage}'");
    }

    [Theory(DisplayName = "Should not log when the endpoint has the IgnoreCustomException attribute")]
    [MemberData(nameof(IgnoredEndpoints))]
    public async Task TryHandleAsync_EndpointWithIgnoreAttribute_ShouldNotLog(
        ExceptionPipeline pipeline, string path, string exceptionMessage)
    {
        // Arrange
        await using var host = await ExceptionPipelineHost.StartAsync(pipeline);

        // Act
        await host.Client.GetAsync(path);

        // Assert
        Assert.DoesNotContain(host.LogCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Exception?.Message == exceptionMessage);
    }

    [Theory(DisplayName = "Should leave the response to the fallback when the endpoint has the IgnoreCustomException attribute")]
    [MemberData(nameof(IgnoredEndpoints))]
    public async Task TryHandleAsync_EndpointWithIgnoreAttribute_ShouldLeaveResponseToFallback(
        ExceptionPipeline pipeline, string path, string exceptionMessage)
    {
        // Arrange
        await using var host = await ExceptionPipelineHost.StartAsync(pipeline);

        // Act
        var response = await host.Client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("UNEXPECTED_ERROR", body);
        if (pipeline == ExceptionPipeline.ProblemDetails)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        else
            Assert.Equal(ExceptionPipelineHost.FallbackPrefix + exceptionMessage, body);
    }

    [Theory(DisplayName = "Should handle and log when the endpoint has no IgnoreCustomException attribute")]
    [MemberData(nameof(Pipelines))]
    public async Task TryHandleAsync_EndpointWithoutIgnoreAttribute_ShouldHandleAndLog(ExceptionPipeline pipeline)
    {
        // Arrange
        const string exceptionMessage = "Something went wrong";
        await using var host = await ExceptionPipelineHost.StartAsync(pipeline);

        // Act
        var response = await host.Client.GetAsync("/product/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<CustomErrorResponse>();
        Assert.NotNull(content);
        Assert.Equal("UNEXPECTED_ERROR", content.Type);
        Assert.DoesNotContain(exceptionMessage, await response.Content.ReadAsStringAsync());
        var entry = Assert.Single(host.LogCollector.Entries, e =>
            e.CategoryName == HandlerCategory && e.Exception?.Message == exceptionMessage);
        Assert.Equal(LogLevel.Error, entry.Level);
    }
}
