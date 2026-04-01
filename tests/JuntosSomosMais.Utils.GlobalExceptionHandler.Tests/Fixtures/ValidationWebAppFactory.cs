using FluentValidation;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestValidators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public class ValidationWebAppFactory : WebApplicationFactory<ValidationWebAppFactory>
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
                            .AddApplicationPart(typeof(ValidationWebAppFactory).Assembly);
                        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
                        services.AddCustomExceptionHandler();
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
