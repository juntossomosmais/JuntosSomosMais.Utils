using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;
    private readonly CustomExceptionHandlerOptions _options;
    private readonly IReadOnlyDictionary<Type, HttpStatusCode> _exceptionMappings;
    private readonly JsonSerializerOptions _serializerOptions;

    private static readonly Dictionary<Type, HttpStatusCode> DefaultMappings = new()
    {
        [typeof(UnauthorizedException)] = HttpStatusCode.Unauthorized,
        [typeof(CannotAccessException)] = HttpStatusCode.Forbidden,
        [typeof(NotFoundException)] = HttpStatusCode.NotFound,
    };

    public CustomExceptionHandler(
        IOptions<CustomExceptionHandlerOptions> options,
        ILogger<CustomExceptionHandler> logger)
    {
        _options = options.Value;
        _exceptionMappings = new Dictionary<Type, HttpStatusCode>(_options.ExceptionMappings);
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

        var statusCode = ResolveStatusCode(exception);
        var exceptionType = ResolveExceptionType(exception);

        _logger.LogError(exception, $"Occurred an exception - TraceId:{httpContext.TraceIdentifier} - ExceptionType:{exceptionType} - Message:{exception.Message}");

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        var responseBody = BuildResponse(httpContext, exception, statusCode, exceptionType);

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(responseBody, _serializerOptions),
            Encoding.UTF8,
            cancellationToken);

        return true;
    }

    private HttpStatusCode ResolveStatusCode(Exception exception)
    {
        var exceptionType = exception.GetType();

        if (_exceptionMappings.TryGetValue(exceptionType, out var custom))
            return custom;

        if (DefaultMappings.TryGetValue(exceptionType, out var builtin))
            return builtin;

        foreach (var (type, code) in _exceptionMappings)
        {
            if (type.IsAssignableFrom(exceptionType))
                return code;
        }

        if (exception is DomainException)
            return HttpStatusCode.BadRequest;

        return HttpStatusCode.InternalServerError;
    }

    private static string ResolveExceptionType(Exception exception)
    {
        if (exception is CustomException customException
            && !string.IsNullOrEmpty(customException.ExceptionType))
        {
            return customException.ExceptionType;
        }

        return exception is CustomException ? "VALIDATION_ERRORS" : "UNEXPECTED_ERROR";
    }

    private object BuildResponse(
        HttpContext httpContext,
        Exception exception,
        HttpStatusCode statusCode,
        string exceptionType)
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

        if (_options.ViewStackTrace)
        {
            return new CustomErrorDetailResponse(
                exceptionType,
                new CustomErrorDetail(exception.Message, exception.StackTrace));
        }

        return new CustomErrorResponse(
            exceptionType,
            new CustomError(exception.Message));
    }
}
