using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class HealthChecksBuilderExtensionsTests
{
    private static (ServiceProvider Provider, HealthCheckRegistration Registration) Register(Action<IHealthChecksBuilder> add)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HistoryHealthRegistry(new[] { (new HistoryOptions("myOrders"), new HistoryHealthState()) }));
        add(services.AddHealthChecks());
        var provider = services.BuildServiceProvider();
        return (provider, Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations));
    }

    [Fact]
    public void AddSimpleHistoryHealthCheck_WithDefaults_ShouldUseTheDefaultNameUnhealthyAndNoTags()
    {
        var (provider, registration) = Register(b => b.AddSimpleHistoryHealthCheck());
        using var _ = provider;

        Assert.Equal("simplehistory", registration.Name);
        Assert.Equal(HealthStatus.Unhealthy, registration.FailureStatus);
        Assert.Empty(registration.Tags);
    }

    [Fact]
    public void AddSimpleHistoryHealthCheck_WithCustomValues_ShouldUseThem()
    {
        var (provider, registration) = Register(b => b.AddSimpleHistoryHealthCheck("history", HealthStatus.Degraded, ["be", "history"]));
        using var _ = provider;

        Assert.Equal("history", registration.Name);
        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        Assert.Equal<string>(["be", "history"], registration.Tags.OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public async Task AddSimpleHistoryHealthCheck_Factory_ShouldResolveTheRegistryFromDi()
    {
        var (provider, registration) = Register(b => b.AddSimpleHistoryHealthCheck());
        using var _ = provider;

        var check = Assert.IsType<HistoryHealthCheck>(registration.Factory(provider));
        var result = await check.CheckHealthAsync(new HealthCheckContext { Registration = registration });

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal<string>(["myOrders"], result.Data.Keys); // the registry registered above
    }

    [Fact]
    public void AddSimpleHistoryHealthCheck_WithoutARegistry_ShouldFailWhenTheCheckIsCreated()
    {
        var services = new ServiceCollection();
        services.AddHealthChecks().AddSimpleHistoryHealthCheck();
        using var provider = services.BuildServiceProvider();
        var registration = Assert.Single(provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);

        Assert.Throws<InvalidOperationException>(() => registration.Factory(provider));
    }
}
