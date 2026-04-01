using System.Text.Encodings.Web;
using System.Text.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var (key, value) in context.ModelState.Where(x => x.Value?.Errors.Count > 0))
                    errors[key] = value!.Errors.Select(e => e.ErrorMessage).ToArray();

                var response = new CustomValidationErrorResponse(
                    "VALIDATION_ERRORS",
                    StatusCodes.Status400BadRequest,
                    errors);

                var serializerOptions = ResolveSerializerOptions(context.HttpContext.RequestServices);
                var json = JsonSerializer.Serialize(response, serializerOptions);

                return new ContentResult
                {
                    Content = json,
                    ContentType = "application/json",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            };
        });

        return services;
    }

    internal static JsonSerializerOptions ResolveSerializerOptions(IServiceProvider serviceProvider)
    {
        var handlerOptions = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<CustomExceptionHandlerOptions>>()?.Value;
        return handlerOptions?.JsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
    }
}
