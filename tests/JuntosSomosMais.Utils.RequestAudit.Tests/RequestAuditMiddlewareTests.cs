using System.Net;
using System.Text;
using JuntosSomosMais.Utils.RequestAudit.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditMiddlewareTests
{
    private const string EchoRoute = "/audited/echo";

    [Fact]
    public async Task InvokeAsync_AuditedJsonRequest_SavesEveryField()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(options => options.ClientIpHeader = "True-Client-IP");
        var request = new HttpRequestMessage(HttpMethod.Post, EchoRoute + "?param1=value1&token=abc")
        {
            Content = Json("""{"key":"value","password":"p"}"""),
        };
        request.Headers.TryAddWithoutValidation("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret");
        request.Headers.TryAddWithoutValidation("True-Client-IP", "203.0.113.7");
        request.Headers.TryAddWithoutValidation(RequestAuditTestHost.UserHeader, "user-1");

        // Act
        var response = await host.Client.SendAsync(request);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"key":"value","password":"p"}""", await response.Content.ReadAsStringAsync());

        var traceId = Assert.Single(response.Headers.GetValues(RequestAuditTestHost.TraceIdHeader));
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(RequestAuditTestHost.Now.UtcDateTime, entry.CreatedAtUtc);
        Assert.Equal("POST", entry.Method);
        Assert.Equal("http://localhost/audited/echo", entry.Url);
        Assert.Equal(new Dictionary<string, string> { ["param1"] = "value1", ["token"] = "***" }, entry.QueryParams);
        Assert.Equal("203.0.113.7", entry.IpAddress);
        Assert.Equal("***", entry.RequestHeaders["Authorization"]);
        Assert.Equal("application/json; charset=utf-8", entry.RequestHeaders["Content-Type"]);
        Assert.Equal("user-1", entry.RequestHeaders[RequestAuditTestHost.UserHeader]);
        Assert.Equal("""{"key":"value","password":"***"}""", entry.RequestBody);
        Assert.Null(entry.RequestBodyNote);
        Assert.Equal(200, entry.ResponseStatusCode);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json; charset=utf-8",
                [RequestAuditTestHost.TraceIdHeader] = traceId,
                ["Set-Cookie"] = "***",
            },
            entry.ResponseHeaders);
        Assert.Equal("""{"key":"value","password":"***"}""", entry.ResponseBody);
        Assert.Null(entry.ResponseBodyNote);
        Assert.Equal(traceId, entry.RequestId);
        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", entry.TraceParent);
        Assert.Equal(new Dictionary<string, string> { ["sub"] = "user-1", ["client_id"] = "client-1" }, entry.Caller);
    }

    [Fact]
    public async Task InvokeAsync_AuditedRequestWithoutBodyOrCaller_SavesTheMinimum()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(options => options.ClientIpHeader = "True-Client-IP");

        // Act
        var response = await host.Client.GetAsync("/audited/empty");
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("GET", entry.Method);
        Assert.Equal("http://localhost/audited/empty", entry.Url);
        Assert.Empty(entry.QueryParams);
        Assert.Null(entry.IpAddress);
        Assert.Null(entry.RequestBody);
        Assert.Null(entry.RequestBodyNote);
        Assert.Equal(204, entry.ResponseStatusCode);
        Assert.Empty(entry.ResponseHeaders);
        Assert.Null(entry.ResponseBody);
        Assert.Null(entry.ResponseBodyNote);
        Assert.Null(entry.TraceParent);
        Assert.Empty(entry.Caller);
    }

    [Fact]
    public async Task InvokeAsync_UrlEncodedForm_SavesTheFieldsMasked()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();
        var content = new StringContent("key1=value1&password=p", Encoding.UTF8, "application/x-www-form-urlencoded");

        // Act
        var response = await host.Client.PostAsync(EchoRoute, content);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal("key1=value1&password=p", await response.Content.ReadAsStringAsync());
        Assert.Equal("""{"key1":"value1","password":"***"}""", entry.RequestBody);
        Assert.Null(entry.RequestBodyNote);
    }

    [Fact]
    public async Task InvokeAsync_ContentTypeThatCannotBeMasked_DoesNotSaveTheBodies()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();
        var content = new StringContent("password=secret", Encoding.UTF8, "text/plain");

        // Act
        var response = await host.Client.PostAsync(EchoRoute, content);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        const string note = "Not stored: content type 'text/plain; charset=utf-8' cannot be masked (15 bytes).";
        Assert.Equal("password=secret", await response.Content.ReadAsStringAsync());
        Assert.Null(entry.RequestBody);
        Assert.Equal(note, entry.RequestBodyNote);
        Assert.Null(entry.ResponseBody);
        Assert.Equal(note, entry.ResponseBodyNote);
    }

    [Fact]
    public async Task InvokeAsync_InvalidJson_DoesNotSaveTheBody()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();

        // Act
        var response = await host.Client.PostAsync(EchoRoute, Json("""{"password":"""));
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal("""{"password":""", await response.Content.ReadAsStringAsync());
        Assert.Null(entry.RequestBody);
        Assert.Equal("Not stored: invalid JSON (12 bytes).", entry.RequestBodyNote);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvokeAsync_BodiesLargerThanTheLimit_AreDeliveredButNotSaved(bool withContentLength)
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(options => options.MaxBodyBytes = 20);
        const string body = """{"key":"a value longer than the limit"}""";
        HttpContent content = withContentLength
            ? Json(body)
            : new StreamContent(new UnknownLengthStream(Encoding.UTF8.GetBytes(body)))
            {
                Headers = { { "Content-Type", "application/json" } },
            };

        // Act
        var response = await host.Client.PostAsync(EchoRoute, content);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
        Assert.Null(entry.RequestBody);
        Assert.Equal("Not stored: larger than 20 bytes.", entry.RequestBodyNote);
        Assert.Null(entry.ResponseBody);
        Assert.Equal("Not stored: larger than 20 bytes.", entry.ResponseBodyNote);
    }

    [Fact]
    public async Task InvokeAsync_BodyOfExactlyTheLimit_IsSaved()
    {
        // Arrange
        const string body = """{"key":"value"}""";
        await using var host = await RequestAuditTestHost.StartAsync(options => options.MaxBodyBytes = body.Length);

        // Act
        var response = await host.Client.PostAsync(EchoRoute, Json(body));
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
        Assert.Equal(body, entry.RequestBody);
        Assert.Equal(body, entry.ResponseBody);
    }

    [Fact]
    public async Task InvokeAsync_ResponseLargerThanTheLimit_ReachesTheCallerWhole()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(options => options.MaxBodyBytes = 50);

        // Act
        var response = await host.Client.GetAsync("/audited/large");
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(RequestAuditTestHost.LargeResponse, await response.Content.ReadAsStringAsync());
        Assert.Equal(200, entry.ResponseStatusCode);
        Assert.Null(entry.ResponseBody);
        Assert.Equal("Not stored: larger than 50 bytes.", entry.ResponseBodyNote);
    }

    [Fact]
    public async Task InvokeAsync_Unauthenticated_SavesThe401()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();

        // Act
        var response = await host.Client.GetAsync("/audited/secure");
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, entry.ResponseStatusCode);
        Assert.Empty(entry.Caller);
    }

    [Fact]
    public async Task InvokeAsync_Forbidden_SavesThe403WithTheCaller()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/audited/admin");
        request.Headers.TryAddWithoutValidation(RequestAuditTestHost.UserHeader, "user-1");

        // Act
        var response = await host.Client.SendAsync(request);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(403, entry.ResponseStatusCode);
        Assert.Equal(new Dictionary<string, string> { ["sub"] = "user-1", ["client_id"] = "client-1" }, entry.Caller);
    }

    [Fact]
    public async Task InvokeAsync_CustomCallerClaims_SavesOnlyThose()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(options =>
        {
            options.CallerClaims.Clear();
            options.CallerClaims.Add("role");
            options.CallerClaims.Add("missing");
        });
        var request = new HttpRequestMessage(HttpMethod.Get, "/audited/admin");
        request.Headers.TryAddWithoutValidation(RequestAuditTestHost.UserHeader, "user-1");
        request.Headers.TryAddWithoutValidation(RequestAuditTestHost.RoleHeader, "admin");

        // Act
        var response = await host.Client.SendAsync(request);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new Dictionary<string, string> { ["role"] = "admin" }, entry.Caller);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionAnsweredByAnEarlierHandler_SavesTheAnswerOfTheHandler()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(withExceptionHandler: true);

        // Act
        var response = await host.Client.PostAsync("/audited/throw", Json("""{"key":"value"}"""));
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(RequestAuditTestHost.HandledErrorBody, await response.Content.ReadAsStringAsync());
        Assert.Equal(422, entry.ResponseStatusCode);
        Assert.Equal("""{"type":"RULE","token":"***"}""", entry.ResponseBody);
        Assert.Equal("""{"key":"value"}""", entry.RequestBody);
    }

    [Fact]
    public async Task InvokeAsync_OnKestrel_SavesTheEntryAfterTheResponseIsSent()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(onKestrel: true);
        var request = new HttpRequestMessage(HttpMethod.Post, EchoRoute + "?page=2") { Content = Json("""{"key":"value","password":"p"}""") };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret");

        // Act
        var response = await host.Client.SendAsync(request);
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"key":"value","password":"p"}""", await response.Content.ReadAsStringAsync());
        Assert.Equal($"{host.Client.BaseAddress}audited/echo", entry.Url);
        Assert.Equal(new Dictionary<string, string> { ["page"] = "2" }, entry.QueryParams);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.Equal("***", entry.RequestHeaders["Authorization"]);
        Assert.Equal("""{"key":"value","password":"***"}""", entry.RequestBody);
        Assert.Equal(200, entry.ResponseStatusCode);
        Assert.Equal("***", entry.ResponseHeaders["Set-Cookie"]);
        Assert.Equal("""{"key":"value","password":"***"}""", entry.ResponseBody);
    }

    [Fact]
    public async Task InvokeAsync_OnKestrelWithAnUnhandledException_SavesThe500OfTheServer()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(onKestrel: true);

        // Act
        var response = await host.Client.PostAsync("/audited/throw", Json("""{"key":"value"}"""));
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(500, entry.ResponseStatusCode);
        Assert.Equal("""{"key":"value"}""", entry.RequestBody);
        Assert.Null(entry.ResponseBody);
        Assert.Null(entry.ResponseBodyNote);
    }

    [Fact]
    public async Task InvokeAsync_OnKestrelWithAnExceptionAnsweredByAnEarlierHandler_SavesTheAnswerOfTheHandler()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync(withExceptionHandler: true, onKestrel: true);

        // Act
        var response = await host.Client.PostAsync("/audited/throw", Json("""{"key":"value"}"""));
        var entry = await host.Store.WaitForEntryAsync();

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(RequestAuditTestHost.HandledErrorBody, await response.Content.ReadAsStringAsync());
        Assert.Equal(422, entry.ResponseStatusCode);
        Assert.Equal("""{"type":"RULE","token":"***"}""", entry.ResponseBody);
    }

    [Fact]
    public async Task InvokeAsync_StoreFails_AnswersNormallyAndLogsTheError()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();
        var failure = new InvalidOperationException("The store is down.");
        host.Store.Failure = failure;

        // Act
        var response = await host.Client.PostAsync(EchoRoute, Json("""{"key":"value"}"""));
        var error = await host.Logs.WaitForErrorAsync<RequestAuditMiddleware>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"key":"value"}""", await response.Content.ReadAsStringAsync());
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Equal("The request audit entry could not be saved.", error.Message);
        Assert.Same(failure, error.Exception);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, null)]
    public async Task InvokeAsync_DisabledOrEndpointNotMarked_LeavesTheRequestUntouched(bool enabled, bool? marked)
    {
        // Arrange
        var store = new Mock<IRequestAuditStore>(MockBehavior.Strict);
        var context = new DefaultHttpContext();
        var requestBody = context.Request.Body;
        var responseBody = context.Response.Body;
        if (marked is not null)
        {
            var metadata = marked.Value ? new object[] { new AuditRequestAttribute() } : [];
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "test"));
        }

        Stream? requestBodySeen = null;
        Stream? responseBodySeen = null;
        var middleware = new RequestAuditMiddleware(
            current =>
            {
                requestBodySeen = current.Request.Body;
                responseBodySeen = current.Response.Body;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new RequestAuditOptions { Enabled = enabled }),
            TimeProvider.System,
            NullLogger<RequestAuditMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Same(requestBody, requestBodySeen);
        Assert.Same(responseBody, responseBodySeen);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_RequestBodyCannotBeRead_SavesTheEntryWithoutIt()
    {
        // Arrange
        RequestAuditEntry? saved = null;
        var store = new Mock<IRequestAuditStore>();
        store.Setup(s => s.SaveAsync(It.IsAny<RequestAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<RequestAuditEntry, CancellationToken>((entry, _) => saved = entry)
            .Returns(Task.CompletedTask);
        var responseFeature = new CompletableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AuditRequestAttribute()), "test"));
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.4");
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Body = new BrokenStream();
        var middleware = new RequestAuditMiddleware(
            current =>
            {
                current.Response.StatusCode = StatusCodes.Status400BadRequest;
                return Task.CompletedTask;
            },
            store.Object,
            Options.Create(new RequestAuditOptions { ClientIpHeader = string.Empty }),
            TimeProvider.System,
            NullLogger<RequestAuditMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);
        await responseFeature.CompleteAsync();

        // Assert
        Assert.NotNull(saved);
        Assert.Null(saved.RequestBody);
        Assert.Equal("Not stored: the request body could not be read.", saved.RequestBodyNote);
        Assert.Equal(400, saved.ResponseStatusCode);
        Assert.Equal("198.51.100.4", saved.IpAddress);
    }

    [Fact]
    public async Task InvokeAsync_EndpointNotMarked_IsAnsweredWithoutAudit()
    {
        // Arrange
        await using var host = await RequestAuditTestHost.StartAsync();
        host.Store.Failure = new InvalidOperationException("Nothing should be saved.");

        // Act
        var response = await host.Client.PostAsync("/plain/echo", Json("""{"key":"value"}"""));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"key":"value"}""", await response.Content.ReadAsStringAsync());
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    /// <summary>A body the caller stopped sending.</summary>
    private sealed class BrokenStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("The client reset the request stream.");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw new IOException("The client reset the request stream.");
    }

    /// <summary>Response feature that runs the completion callbacks when the test says the response was sent.</summary>
    private sealed class CompletableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _completed = [];

        public override void OnCompleted(Func<object, Task> callback, object state) => _completed.Add((callback, state));

        public async Task CompleteAsync()
        {
            foreach (var (callback, state) in _completed)
                await callback(state);
        }
    }

    /// <summary>A body whose length is unknown beforehand, so it is sent without Content-Length.</summary>
    private sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
