using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

/// <summary>
/// The ways a service can wire <c>UseExceptionHandler</c> around the <see cref="CustomExceptionHandler"/>.
/// </summary>
public enum ExceptionPipeline
{
    /// <summary><c>UseExceptionHandler(options with ExceptionHandler delegate)</c> before <c>UseRouting</c>.</summary>
    DelegateBeforeRouting,

    /// <summary><c>UseRouting</c> first, then <c>UseExceptionHandler(options with ExceptionHandler delegate)</c>.</summary>
    DelegateAfterRouting,

    /// <summary><c>UseExceptionHandler("/error")</c>: the fallback re-executes the pipeline on an error path.</summary>
    ErrorPath,

    /// <summary><c>AddProblemDetails()</c> and a bare <c>UseExceptionHandler()</c>: the fallback is the ProblemDetails writer.</summary>
    ProblemDetails,
}

/// <summary>
/// Builds a real ASP.NET Core pipeline on a <see cref="TestServer"/> for one <see cref="ExceptionPipeline"/>, with
/// <c>ViewStackTrace</c> off, so a body written by <see cref="CustomExceptionHandler"/> never carries the exception
/// message, and a fallback whose body always does.
/// </summary>
public sealed class ExceptionPipelineHost : IAsyncDisposable
{
    public const string FallbackPrefix = "fallback:";
    public const string MinimalIgnorePath = "/minimal/ignore";
    public const string MinimalIgnoreMessage = "Some error ignore minimal endpoint";

    private readonly WebApplication _app;

    private ExceptionPipelineHost(WebApplication app, FakeLogCollector logCollector)
    {
        _app = app;
        LogCollector = logCollector;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }
    public FakeLogCollector LogCollector { get; }

    public static async Task<ExceptionPipelineHost> StartAsync(ExceptionPipeline pipeline)
    {
        var logCollector = new FakeLogCollector();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Test",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddProvider(new FakeLoggerProvider(logCollector));

        builder.Services.AddControllers().AddApplicationPart(typeof(ExceptionPipelineHost).Assembly);
        builder.Services.AddCustomExceptionHandler(options => options.ViewStackTrace = false);
        if (pipeline == ExceptionPipeline.ProblemDetails)
            builder.Services.AddProblemDetails();

        var app = builder.Build();

        switch (pipeline)
        {
            case ExceptionPipeline.DelegateBeforeRouting:
                app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = WriteFallbackAsync });
                app.UseRouting();
                break;
            case ExceptionPipeline.DelegateAfterRouting:
                app.UseRouting();
                app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandler = WriteFallbackAsync });
                break;
            case ExceptionPipeline.ErrorPath:
                app.UseExceptionHandler("/error");
                app.UseRouting();
                app.Map("/error", WriteFallbackAsync);
                break;
            case ExceptionPipeline.ProblemDetails:
                app.UseExceptionHandler();
                app.UseRouting();
                break;
        }

        app.MapControllers();
        app.MapGet(MinimalIgnorePath, ThrowFromMinimalEndpoint).WithMetadata(new IgnoreCustomExceptionAttribute());

        await app.StartAsync();
        return new ExceptionPipelineHost(app, logCollector);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private static IResult ThrowFromMinimalEndpoint() => throw new Exception(MinimalIgnoreMessage);

    private static async Task WriteFallbackAsync(HttpContext context)
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(FallbackPrefix + feature?.Error.Message);
    }
}
