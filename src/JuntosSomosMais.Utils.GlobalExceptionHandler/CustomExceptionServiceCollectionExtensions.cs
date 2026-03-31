using Microsoft.Extensions.DependencyInjection;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public static class CustomExceptionServiceCollectionExtensions
{
    public static IServiceCollection AddCustomExceptionHandler(
        this IServiceCollection services)
    {
        return services.AddCustomExceptionHandler(_ => { });
    }

    public static IServiceCollection AddCustomExceptionHandler(
        this IServiceCollection services,
        Action<CustomExceptionHandlerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddExceptionHandler<CustomExceptionHandler>();
        return services;
    }
}
