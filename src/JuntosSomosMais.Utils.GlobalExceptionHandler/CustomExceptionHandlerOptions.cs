using System.Net;
using System.Text.Json;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public class CustomExceptionHandlerOptions
{
    /// <summary>
    /// When true, includes the stack trace in the response body under "error.detail".
    /// Default: false. Recommended only for Development environments.
    /// </summary>
    public bool ViewStackTrace { get; set; }

    /// <summary>
    /// Additional exception-to-status-code mappings beyond the built-in ones.
    /// Consumer-provided mappings are checked before built-in ones, allowing overrides.
    /// </summary>
    public Dictionary<Type, HttpStatusCode> ExceptionMappings { get; set; } = new();

    /// <summary>
    /// Custom JSON serializer options. Defaults to camelCase with relaxed encoding.
    /// </summary>
    public JsonSerializerOptions? JsonSerializerOptions { get; set; }

    /// <summary>
    /// Optional delegate to replace the default response body.
    /// When set, the returned object is serialized instead of CustomErrorResponse/CustomErrorDetailResponse.
    /// </summary>
    public Func<CustomExceptionContext, object>? CustomizeResponse { get; set; }
}
