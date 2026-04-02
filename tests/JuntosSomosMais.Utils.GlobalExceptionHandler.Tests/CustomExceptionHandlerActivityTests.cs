using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

[Collection("ActivityTests")]
public class CustomExceptionHandlerActivityTests : IClassFixture<ActivityWebAppFactory>
{
    private readonly HttpClient _client;

    public CustomExceptionHandlerActivityTests(ActivityWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Should enrich Activity with exception tags when a general 500 exception is thrown")]
    public async Task TryHandleAsync_GeneralException_ShouldEnrichActivityWithExceptionTags()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;

        // Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        AssertActivityHasExceptionTags(activity, typeof(Exception).FullName!, "Custom exception message");
    }

    [Fact(DisplayName = "Should set ActivityStatusCode.Error for 500 server error exceptions")]
    public async Task TryHandleAsync_ServerErrorException_ShouldSetActivityStatusCodeError()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;

        // Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Custom exception message", activity.StatusDescription);
    }

    [Fact(DisplayName = "Should NOT set ActivityStatusCode.Error for validation exceptions (400)")]
    public async Task TryHandleAsync_ValidationException_ShouldNotSetActivityStatusCodeError()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;
        var request = new { Name = "John", Email = "not-an-email" };

        // Act
        var response = await _client.PostAsJsonAsync("/person/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
    }

    [Fact(DisplayName = "Should enrich Activity with exception tags for validation exceptions")]
    public async Task TryHandleAsync_ValidationException_ShouldEnrichActivityWithExceptionTags()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;
        var request = new { Name = "John", Email = "not-an-email" };

        // Act
        var response = await _client.PostAsJsonAsync("/person/validate-and-throw", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        var exceptionType = GetTagValue(activity, "exception.type");
        var exceptionMessage = GetTagValue(activity, "exception.message");
        Assert.Equal("FluentValidation.ValidationException", exceptionType);
        Assert.NotNull(exceptionMessage);
        Assert.NotEmpty(exceptionMessage);
    }

    [Fact(DisplayName = "Should NOT set ActivityStatusCode.Error for attributed exceptions with non-5xx status codes")]
    public async Task TryHandleAsync_AttributedException404_ShouldNotSetActivityStatusCodeError()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;

        // Act
        var response = await _client.GetAsync("/customer/not-found");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        AssertActivityHasExceptionTags(activity, typeof(NotFoundException).FullName!, "Customer not found");
        Assert.NotEqual(ActivityStatusCode.Error, activity.Status);
    }

    [Fact(DisplayName = "Should set ActivityStatusCode.Error for attributed exceptions with 5xx status codes")]
    public async Task TryHandleAsync_AttributedException503_ShouldSetActivityStatusCodeError()
    {
        // Arrange
        var (listener, stoppedActivities) = CreateActivityListener();
        using var _ = listener;

        // Act
        var response = await _client.GetAsync("/customer/custom-typed");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var activity = Assert.Single(stoppedActivities);
        AssertActivityHasExceptionTags(activity, typeof(CustomTypedException).FullName!, "Service is down");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Service is down", activity.StatusDescription);
    }

    [Fact(DisplayName = "Should not throw when no Activity is active")]
    public async Task TryHandleAsync_NoActivityActive_ShouldNotThrow()
    {
        // Arrange - use BasicWebAppFactory behavior through a separate client without activity middleware
        // The ActivityWebAppFactory middleware always starts an activity, so we test the no-op path
        // by verifying a successful error response is still returned (no crash from null Activity)
        // The middleware creates the activity, but if we don't register a listener, the activity
        // source won't produce activities (no listeners = no activities created)

        // Act
        var response = await _client.GetAsync("/customer/exception");

        // Assert - the request should complete successfully with the expected error response
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static void AssertActivityHasExceptionTags(Activity activity, string expectedType, string expectedMessage)
    {
        var exceptionType = GetTagValue(activity, "exception.type");
        var exceptionMessage = GetTagValue(activity, "exception.message");
        var stacktrace = GetTagValue(activity, "exception.stacktrace");

        Assert.Equal(expectedType, exceptionType);
        Assert.Equal(expectedMessage, exceptionMessage);
        Assert.NotNull(stacktrace);
        Assert.Contains(expectedType, stacktrace);
        Assert.Contains(expectedMessage, stacktrace);
    }

    private static (ActivityListener listener, ConcurrentBag<Activity> stoppedActivities) CreateActivityListener()
    {
        var stoppedActivities = new ConcurrentBag<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ActivityWebAppFactory.TestActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, stoppedActivities);
    }

    private static string? GetTagValue(Activity activity, string key)
    {
        foreach (var tag in activity.Tags)
        {
            if (tag.Key == key)
                return tag.Value;
        }

        return null;
    }
}
