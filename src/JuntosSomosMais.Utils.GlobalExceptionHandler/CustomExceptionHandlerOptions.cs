using System.Text.Json;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public class CustomExceptionHandlerOptions
{
    /// <summary>
    /// When true, includes the full exception details (message + stack trace) in the response body under "error.detail".
    /// Default: false. Recommended only for Development environments.
    /// </summary>
    public bool ViewStackTrace { get; set; }

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
