using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

    // Called outside UseExceptionHandler (no IExceptionHandlerFeature): the context endpoint is the only source.
    [Fact(DisplayName = "Should not handle when there is no exception feature and the context endpoint has the attribute")]
    public async Task TryHandleAsync_NoFeatureAndContextEndpointWithAttribute_ShouldNotHandle()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(CreateEndpoint(new IgnoreCustomExceptionAttribute()));

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new Exception("boom"), CancellationToken.None);

        // Assert
        Assert.False(handled);
        Assert.False(httpContext.Response.HasStarted);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact(DisplayName = "Should fall back to the context endpoint when the exception feature has no endpoint")]
    public async Task TryHandleAsync_FeatureWithoutEndpointAndContextEndpointWithAttribute_ShouldNotHandle()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = new DefaultHttpContext();
        var exception = new Exception("boom");
        httpContext.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception, Endpoint = null });
        httpContext.SetEndpoint(CreateEndpoint(new IgnoreCustomExceptionAttribute()));

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.False(handled);
    }

    [Fact(DisplayName = "Should handle when neither the exception feature nor the context has an endpoint")]
    public async Task TryHandleAsync_NoEndpointAnywhere_ShouldHandle()
    {
        // Arrange
        var handler = CreateHandler();
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new Exception("boom");
        httpContext.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception, Endpoint = null });

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }

    private static CustomExceptionHandler CreateHandler() =>
        new(Options.Create(new CustomExceptionHandlerOptions()), NullLogger<CustomExceptionHandler>.Instance);

    private static Endpoint CreateEndpoint(params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test endpoint");
}
