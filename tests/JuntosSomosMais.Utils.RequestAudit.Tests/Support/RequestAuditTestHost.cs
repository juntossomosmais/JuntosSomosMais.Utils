using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.RequestAudit.Tests.Support;

/// <summary>
/// Minimal app with the audit middleware in its documented position (after routing, before authentication), a store
/// kept in memory and a few endpoints. It depends on nothing but ASP.NET Core and the library. It runs on the
/// in-memory test server, or on Kestrel (a free local port) to cover what only a real server does.
/// </summary>
internal sealed class RequestAuditTestHost : IAsyncDisposable
{
    public const string UserHeader = "X-Test-User";
    public const string RoleHeader = "X-Test-Role";
    public const string TraceIdHeader = "X-Trace-Id";
    public const string LargeResponse = """{"data":"0123456789012345678901234567890123456789012345678901234567890123456789"}""";
    public const string HandledErrorBody = """{"type":"RULE","token":"abc"}""";

    public static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 30, 0, TimeSpan.Zero);

    private readonly IHost _host;

    private RequestAuditTestHost(IHost host, InMemoryRequestAuditStore store, RecordingLoggerProvider logs, bool onKestrel)
    {
        _host = host;
        Store = store;
        Logs = logs;
        Client = onKestrel
            ? new HttpClient { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) }
            : host.GetTestClient();
    }

    public InMemoryRequestAuditStore Store { get; }
    public RecordingLoggerProvider Logs { get; }
    public HttpClient Client { get; }

    public static async Task<RequestAuditTestHost> StartAsync(
        Action<RequestAuditOptions>? configure = null, bool withExceptionHandler = false, bool onKestrel = false)
    {
        var store = new InMemoryRequestAuditStore();
        var logs = new RecordingLoggerProvider();
        var host = await new HostBuilder()
            .ConfigureWebHost(web => (onKestrel ? web.UseKestrel().UseUrls("http://127.0.0.1:0") : web.UseTestServer())
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging(logging => logging.AddProvider(logs));
                    services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, null);
                    services.AddAuthorization(options => options.AddPolicy("admin", policy => policy.RequireClaim("role", "admin")));
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                    services.AddRequestAudit(configure);
                    services.AddSingleton<IRequestAuditStore>(store);
                })
                .Configure(app =>
                {
                    if (withExceptionHandler)
                        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = WriteHandledErrorAsync });

                    app.UseRouting();
                    app.UseRequestAudit();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(MapEndpoints);
                }))
            .StartAsync();

        return new RequestAuditTestHost(host, store, logs, onKestrel);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private static void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var audited = new AuditRequestAttribute();
        endpoints.MapPost("/audited/echo", EchoAsync).WithMetadata(audited);
        endpoints.MapPost("/plain/echo", EchoAsync);
        endpoints.MapGet("/audited/empty", WriteNoContentAsync).WithMetadata(audited);
        endpoints.MapGet("/audited/large", WriteLargeAsync).WithMetadata(audited);
        endpoints.MapPost("/audited/throw", ThrowAsync).WithMetadata(audited);
        endpoints.MapGet("/audited/secure", WriteNoContentAsync).RequireAuthorization().WithMetadata(audited);
        endpoints.MapGet("/audited/admin", WriteNoContentAsync).RequireAuthorization("admin").WithMetadata(audited);
    }

    // Disposes the request body on purpose: the audit must not depend on what the endpoint does with it.
    private static async Task EchoAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        context.Response.ContentType = context.Request.ContentType ?? "text/plain";
        context.Response.Headers[TraceIdHeader] = context.TraceIdentifier;
        context.Response.Headers.SetCookie = "session=abc";
        await context.Response.WriteAsync(body);
    }

    private static Task WriteNoContentAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }

    private static async Task WriteLargeAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(LargeResponse[..40]);
        await context.Response.WriteAsync(LargeResponse[40..]);
    }

    private static Task ThrowAsync(HttpContext context) => throw new InvalidOperationException("The endpoint failed.");

    private static async Task WriteHandledErrorAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(HandledErrorBody);
    }
}

/// <summary>Keeps the entries in memory. The entry is saved after the response is sent, so tests wait for it.</summary>
internal sealed class InMemoryRequestAuditStore : IRequestAuditStore
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource<RequestAuditEntry> _saved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>When set, every save fails with it.</summary>
    public Exception? Failure { get; set; }

    public Task SaveAsync(RequestAuditEntry entry, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
            throw Failure;

        _saved.TrySetResult(entry);
        return Task.CompletedTask;
    }

    public Task<RequestAuditEntry> WaitForEntryAsync() => _saved.Task.WaitAsync(Timeout);
}

/// <summary>Authenticates a request carrying the user header; its value becomes the <c>sub</c> claim.</summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RequestAuditTestHost.UserHeader, out var user))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new("sub", user.ToString()), new("client_id", "client-1"), new("email", "user@example.com") };
        if (Request.Headers.TryGetValue(RequestAuditTestHost.RoleHeader, out var role))
            claims.Add(new Claim("role", role.ToString()));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Records what is logged and lets a test wait for the first error of a category.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<LogRecord>> _errors = new();

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

    public Task<LogRecord> WaitForErrorAsync<TCategory>() => ErrorOf(typeof(TCategory).FullName!).Task.WaitAsync(Timeout);

    public void Dispose()
    {
        // Nothing to release.
    }

    private TaskCompletionSource<LogRecord> ErrorOf(string category) =>
        _errors.GetOrAdd(category, _ => new TaskCompletionSource<LogRecord>(TaskCreationOptions.RunContinuationsAsynchronously));

    internal sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger(RecordingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                provider.ErrorOf(category).TrySetResult(new LogRecord(logLevel, formatter(state, exception), exception));
        }
    }
}
