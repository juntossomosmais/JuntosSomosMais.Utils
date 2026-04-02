using System.Diagnostics;
using FluentValidation;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestValidators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public class ActivityWebAppFactory : WebApplicationFactory<ActivityWebAppFactory>
{
    public const string TestActivitySourceName = "TestSource.ExceptionHandler";
    public static readonly ActivitySource TestActivitySource = new(TestActivitySourceName);

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
                            .AddApplicationPart(typeof(ActivityWebAppFactory).Assembly);
                        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
                        services.AddCustomExceptionHandler();
                    })
                    .Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            using var activity = TestActivitySource.StartActivity("TestRequest");
                            if (activity is not null)
                            {
                                context.Items["TestActivity"] = activity;
                            }

                            await next();
                        });
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
