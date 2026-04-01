using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public class CustomResponseWebAppFactory : WebApplicationFactory<CustomResponseWebAppFactory>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        var host = builder.Build();
        host.Start();
        return host;
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        return new HostBuilder()
            .ConfigureWebHostDefaults(webHostBuilder =>
            {
                webHostBuilder
                    .UseContentRoot(AppContext.BaseDirectory)
                    .UseEnvironment("Test")
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddControllers()
                            .AddApplicationPart(typeof(CustomResponseWebAppFactory).Assembly);
                        services.AddCustomExceptionHandler(options =>
                        {
                            options.JsonSerializerOptions = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                                WriteIndented = false
                            };
                            options.CustomizeResponse = ctx => new
                            {
                                TraceId = ctx.HttpContext.TraceIdentifier,
                                ErrorMessage = ctx.Exception.Message,
                                ResolvedStatusCode = ctx.StatusCode,
                                ResolvedExceptionType = ctx.ExceptionType
                            };
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseExceptionHandler(new ExceptionHandlerOptions
                        {
                            ExceptionHandler = _ => Task.CompletedTask
                        });
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }
}
