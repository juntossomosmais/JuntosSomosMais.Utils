using JuntosSomosMais.Utils.SimpleHistory.MongoDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryRegistrationTests
{
    private static ServiceProvider Build(Action<MongoHistoryBuilder> configure, Func<IServiceProvider, IMongoDatabase>? database = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddMongoHistory(database ?? (_ => new Mock<IMongoDatabase>().Object), configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMongoHistory_WithTwoSources_ShouldRegisterTwoHostedServicesAndOneSourcesList()
    {
        using var provider = Build(h => h.Track("orders", o => o.IgnoreFields("CreationDate")).Track("carts"));

        var hosted = provider.GetServices<IHostedService>().ToList();

        Assert.Equal(2, hosted.Count);
        Assert.All(hosted, h => Assert.IsType<HistoryHostedService>(h));
        Assert.NotSame(hosted[0], hosted[1]);
        var sources = provider.GetRequiredService<HistorySources>();
        Assert.Equal(new[] { "orders", "carts" }, sources.Options.Select(o => o.SourceName));
        Assert.Equal(new[] { "ordersHistory", "cartsHistory" }, sources.Options.Select(o => o.HistoryName));
    }

    [Fact]
    public void AddMongoHistory_ShouldIsolateTheOptionsOfEachSource()
    {
        using var provider = Build(h => h
            .Track("orders", o => { o.IgnoreFields("CreationDate"); o.BatchSize = 5; o.CompressHistory = false; })
            .Track("carts"));

        var sources = provider.GetRequiredService<HistorySources>().Options;

        Assert.Contains("CreationDate", sources[0].IgnoredFields);
        Assert.Empty(sources[1].IgnoredFields);
        Assert.Equal(5, sources[0].BatchSize);
        Assert.Equal(100, sources[1].BatchSize);
        Assert.False(((MongoHistoryOptions)sources[0]).CompressHistory);
        Assert.True(((MongoHistoryOptions)sources[1]).CompressHistory);
    }

    [Fact]
    public void AddMongoHistory_ShouldRegisterOneHealthRegistryWithOneEntryPerSource()
    {
        using var provider = Build(h => h.Track("orders").Track("carts"));

        var registry = provider.GetRequiredService<HistoryHealthRegistry>();

        Assert.Same(registry, provider.GetRequiredService<HistoryHealthRegistry>());
        Assert.Equal(new[] { "orders", "carts" }, registry.Sources.Select(s => s.Options.SourceName));
        Assert.NotSame(registry.Sources[0].State, registry.Sources[1].State);
        // The registry holds the very options the hosted services use.
        Assert.Same(provider.GetRequiredService<HistorySources>().Options[0], registry.Sources[0].Options);
    }

    [Fact]
    public void AddMongoHistory_Alone_ShouldNotRegisterAnyHealthCheck()
    {
        using var provider = Build(h => h.Track("orders"));

        Assert.Null(provider.GetService<IOptions<HealthCheckServiceOptions>>()?.Value.Registrations.FirstOrDefault());
        Assert.Null(provider.GetService<HealthCheckService>());
    }

    [Fact]
    public async Task AddSimpleHistoryHealthCheck_AfterAddMongoHistory_ShouldBeHealthyRightAway()
    {
        var services = new ServiceCollection();
        services.AddMongoHistory(_ => new Mock<IMongoDatabase>().Object, h => h.Track("orders"));
        services.AddHealthChecks().AddSimpleHistoryHealthCheck();
        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);

        var result = await registration.Factory(provider).CheckHealthAsync(new HealthCheckContext { Registration = registration });

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(new[] { "orders" }, result.Data.Keys);
    }

    [Fact]
    public void AddMongoHistory_ShouldRegisterTheMongoPurger()
    {
        using var provider = Build(h => h.Track("orders"));

        Assert.IsType<MongoHistoryPurger>(provider.GetRequiredService<IHistoryPurger>());
    }

    [Fact]
    public void AddMongoHistory_ShouldNotResolveTheDatabaseAtRegistration()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        services.AddMongoHistory(_ => { calls++; return new Mock<IMongoDatabase>().Object; }, h => h.Track("orders"));
        using var provider = services.BuildServiceProvider();
        Assert.Equal(0, calls);

        _ = provider.GetServices<IHostedService>().ToList();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void AddMongoHistory_WithoutLoggerFactory_ShouldStillCreateTheHostedService()
    {
        var services = new ServiceCollection();
        services.AddMongoHistory(_ => new Mock<IMongoDatabase>().Object, h => h.Track("orders"));
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IHostedService>());
    }

    [Fact]
    public void Track_ShouldReturnTheMongoBuilder()
    {
        var builder = new MongoHistoryBuilder();

        var returned = builder.Track("orders").Track("carts", o => o.CompressHistory = false);

        Assert.Same(builder, returned);
        var sources = returned.Build(); // typed IReadOnlyList<MongoHistoryOptions> only through the Mongo builder
        Assert.Equal(new[] { "orders", "carts" }, sources.Select(o => o.SourceName));
        Assert.Equal(new[] { true, false }, sources.Select(o => o.CompressHistory));
    }

    [Fact]
    public void AddMongoHistory_WithInvalidOptions_ShouldThrowAtRegistration()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h.Track("orders", o => o.BatchSize = 0)));
    }
}
