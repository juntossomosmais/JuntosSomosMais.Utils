using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb;

/// <summary>Registration of the history hosted services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Tracks the collections described by <paramref name="configure"/>: one hosted service per collection (setup,
    /// change stream), plus a <see cref="HistorySources"/> singleton, a <see cref="HistoryHealthRegistry"/> singleton
    /// and the <see cref="IHistoryPurger"/>. No health check is registered: opt in with
    /// <c>services.AddHealthChecks().AddSimpleHistoryHealthCheck()</c>. Call it only in the deployment that should
    /// consume the streams, and only once: track every collection in the same <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="database">Resolves the database lazily, when the hosted services are created.</param>
    /// <param name="configure">Declares the tracked collections.</param>
    /// <exception cref="InvalidOperationException">
    /// The configuration is invalid, or SimpleHistory is already registered (checked at registration).
    /// </exception>
    public static IServiceCollection AddMongoHistory(
        this IServiceCollection services,
        Func<IServiceProvider, IMongoDatabase> database,
        Action<MongoHistoryBuilder> configure)
    {
        if (services.Any(d => d.ServiceType == typeof(HistorySources)))
            throw new InvalidOperationException("AddMongoHistory was already called: track every collection in one call.");

        var builder = new MongoHistoryBuilder();
        configure(builder);
        var sources = builder.Build();

        services.AddSingleton(new HistorySources(sources));
        services.AddSingleton<IHistoryPurger>(sp => new MongoHistoryPurger(database(sp)));
        var health = new List<(HistoryOptions Options, HistoryHealthState State)>(sources.Count);
        foreach (var options in sources)
        {
            var state = new HistoryHealthState();
            health.Add((options, state));
            services.AddSingleton<IHostedService>(sp =>
            {
                var db = database(sp);
                var loggerFactory = sp.GetService<ILoggerFactory>();
                var checkpoints = new MongoHistoryCheckpointStore(db, options.CheckpointName, options.CheckpointId);
                var processor = new MongoHistoryProcessor(
                    db, options, checkpoints, state, loggerFactory?.CreateLogger<MongoHistoryProcessor>() ?? (ILogger)NullLogger.Instance);
                return new HistoryHostedService(
                    new MongoHistorySetup(db, options),
                    processor,
                    loggerFactory?.CreateLogger<HistoryHostedService>() ?? (ILogger)NullLogger.Instance,
                    options,
                    state);
            });
        }

        services.AddSingleton(new HistoryHealthRegistry(health));
        return services;
    }
}
