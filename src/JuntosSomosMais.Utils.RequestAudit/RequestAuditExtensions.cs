using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Registration of the request auditing.</summary>
public static class RequestAuditExtensions
{
    /// <summary>
    /// Registers the options of the request auditing. A storage provider must also be registered (for example
    /// <c>AddMongoRequestAudit</c>).
    /// </summary>
    public static IServiceCollection AddRequestAudit(
        this IServiceCollection services, Action<RequestAuditOptions>? configure = null)
    {
        var builder = services.AddOptions<RequestAuditOptions>()
            .Validate(options => options.MaxBodyBytes > 0, "RequestAuditOptions.MaxBodyBytes must be positive.");
        if (configure is not null)
            builder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>
    /// Audits the endpoints marked with <see cref="AuditRequestAttribute"/>. Call it after <c>UseRouting</c> and
    /// before <c>UseAuthentication</c>.
    /// </summary>
    public static IApplicationBuilder UseRequestAudit(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestAuditMiddleware>();
}
