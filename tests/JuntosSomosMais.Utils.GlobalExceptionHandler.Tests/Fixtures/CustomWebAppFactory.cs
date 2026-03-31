using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public class CustomWebAppFactory : WebApplicationFactory<CustomWebAppFactory>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // WebApplicationFactory.ConfigureHostBuilder sets content root to
        // <solutionRoot>/<assemblyName>/ which does not exist in our layout.
        // Re-set the content root to the output directory before building.
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
                            .AddApplicationPart(typeof(CustomWebAppFactory).Assembly);
                        services.AddCustomExceptionHandler(options =>
                        {
                            options.ViewStackTrace = true;
                            options.ExceptionMappings[typeof(ConflictTestException)] = HttpStatusCode.Conflict;
                            options.ExceptionMappings[typeof(BaseCustomException)] = HttpStatusCode.UnprocessableEntity;
                        });
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseExceptionHandler(new ExceptionHandlerOptions
                        {
                            ExceptionHandler = async ctx =>
                            {
                                var feature = ctx.Features.Get<IExceptionHandlerFeature>();
                                if (feature?.Error is not null)
                                {
                                    ctx.Response.ContentType = "text/plain";
                                    await ctx.Response.WriteAsync(feature.Error.Message);
                                }
                            }
                        });
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }
}
