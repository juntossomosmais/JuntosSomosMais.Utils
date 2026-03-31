using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public class BasicWebAppFactory : WebApplicationFactory<BasicWebAppFactory>
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
                            .AddApplicationPart(typeof(BasicWebAppFactory).Assembly);
                        services.AddCustomExceptionHandler();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseExceptionHandler(new ExceptionHandlerOptions
                        {
                            ExceptionHandler = _ => Task.CompletedTask
                        });
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            });
    }
}
