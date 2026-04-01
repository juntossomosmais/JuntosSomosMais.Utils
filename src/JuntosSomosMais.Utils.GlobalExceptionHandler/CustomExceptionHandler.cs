using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public class CustomExceptionHandler : IExceptionHandler
{
    private const string FriendlyMessage = "Oh, sorry! We didn't expect that \U0001f62c Please inform the ID \"{0}\" so we can help you properly.";
    private const string DefaultExceptionType = "UNEXPECTED_ERROR";
    private const int DefaultErrorStatusCode = StatusCodes.Status500InternalServerError;

    private static readonly ConcurrentDictionary<Type, ExceptionStatusCodeAttribute?> AttributeCache = new();

    private static readonly FrozenDictionary<int, string> DefaultExceptionTypesByStatusCode = new Dictionary<int, string>
    {
        [400] = "VALIDATION_ERRORS",
        [401] = "UNAUTHORIZED_ERROR",
        [403] = "FORBIDDEN_ERROR",
        [404] = "NOT_FOUND_ERROR",
        [503] = "SERVICE_UNAVAILABLE_ERROR",
    }.ToFrozenDictionary();

    private readonly ILogger<CustomExceptionHandler> _logger;
    private readonly CustomExceptionHandlerOptions _options;
    private readonly JsonSerializerOptions _serializerOptions;

    public CustomExceptionHandler(
        IOptions<CustomExceptionHandlerOptions> options,
        ILogger<CustomExceptionHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
        _serializerOptions = _options.JsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        _serializerOptions.MakeReadOnly(populateMissingResolver: true);
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IgnoreCustomExceptionAttribute>() is not null)
            return false;

        var requestId = httpContext.TraceIdentifier;

        _logger.LogError(exception, "Occurred an exception - TraceId:{TraceId} - Message:{Message}", requestId, exception.Message);

        var attr = GetExceptionStatusCodeAttribute(exception.GetType());
        var statusCode = attr?.StatusCode ?? DefaultErrorStatusCode;
        var exceptionType = attr is not null
            ? attr.ExceptionType ?? GetDefaultExceptionType(attr.StatusCode)
            : DefaultExceptionType;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var responseBody = BuildResponse(httpContext, exception, statusCode, exceptionType);

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(responseBody, _serializerOptions),
            Encoding.UTF8,
            cancellationToken);

        return true;
    }

    private object BuildResponse(HttpContext httpContext, Exception exception, int statusCode, string exceptionType)
    {
        if (_options.CustomizeResponse is not null)
        {
            return _options.CustomizeResponse(new CustomExceptionContext
            {
                HttpContext = httpContext,
                Exception = exception,
                StatusCode = statusCode,
                ExceptionType = exceptionType
            });
        }

        var requestId = httpContext.TraceIdentifier;
        var friendlyMsg = string.Format(FriendlyMessage, requestId);

        if (_options.ViewStackTrace)
        {
            return new CustomErrorDetailResponse(
                exceptionType,
                statusCode,
                new CustomErrorDetail(requestId, friendlyMsg, exception.ToString()));
        }

        return new CustomErrorResponse(
            exceptionType,
            statusCode,
            new CustomError(requestId, friendlyMsg));
    }

    private static ExceptionStatusCodeAttribute? GetExceptionStatusCodeAttribute(Type exceptionType)
    {
        return AttributeCache.GetOrAdd(exceptionType, static type =>
            Attribute.GetCustomAttribute(type, typeof(ExceptionStatusCodeAttribute), inherit: true) as ExceptionStatusCodeAttribute);
    }

    private static string GetDefaultExceptionType(int statusCode)
    {
        return DefaultExceptionTypesByStatusCode.TryGetValue(statusCode, out var type)
            ? type
            : DefaultExceptionType;
    }
}
