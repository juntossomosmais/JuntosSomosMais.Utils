using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <summary>Registration of the MongoDB provider of the request auditing.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Stores the audit entries in MongoDB and registers the <see cref="IRequestAuditPurger"/>. It does not create
    /// indexes: run <see cref="MongoRequestAuditSetup.EnsureAsync"/> from the tool that prepares the database.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="database">Resolves the database lazily, when the store is first used.</param>
    /// <param name="configure">Optional collection name.</param>
    /// <exception cref="InvalidOperationException">The options are invalid, or a store is already registered.</exception>
    public static IServiceCollection AddMongoRequestAudit(
        this IServiceCollection services,
        Func<IServiceProvider, IMongoDatabase> database,
        Action<MongoRequestAuditOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IRequestAuditStore)))
            throw new InvalidOperationException("A request audit store is already registered.");

        var options = new MongoRequestAuditOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IRequestAuditStore>(provider => new MongoRequestAuditStore(database(provider), options));
        services.AddSingleton<IRequestAuditPurger>(provider => new MongoRequestAuditPurger(database(provider), options));
        return services;
    }
}
