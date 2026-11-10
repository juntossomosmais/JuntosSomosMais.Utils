using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>
/// Records the requests to the endpoints marked with <see cref="AuditRequestAttribute"/>. It must run after routing
/// (the endpoint is known) and before authentication (a 401 or 403 is recorded too).
/// </summary>
public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    IRequestAuditStore store,
    IOptions<RequestAuditOptions> options,
    TimeProvider timeProvider,
    ILogger<RequestAuditMiddleware> logger)
{
    private const string TraceParentHeader = "traceparent";

    private readonly RequestAuditOptions _options = options.Value;

    /// <summary>Runs the middleware.</summary>
    public Task InvokeAsync(HttpContext context) =>
        _options.Enabled && context.GetEndpoint()?.Metadata.GetMetadata<AuditRequestAttribute>() is not null
            ? AuditAsync(context)
            : next(context);

    private async Task AuditAsync(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var audit = new PendingAudit
        {
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            Method = request.Method,
            Url = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}",
            QueryParams = RequestAuditMasker.MaskFields(request.Query, _options),
            IpAddress = ReadIpAddress(context),
            RequestHeaders = RequestAuditMasker.MaskHeaders(request.Headers, _options),
            RequestId = context.TraceIdentifier,
            TraceParent = request.Headers.TryGetValue(TraceParentHeader, out var traceParent) ? traceParent.ToString() : null,
        };

        // Read before the endpoint runs: afterwards the stream may be disposed or replaced.
        (audit.RequestBody, audit.RequestBodyNote) = await ReadRequestBodyAsync(request);

        var originalBody = response.Body;
        var capture = new CapturingStream(originalBody, _options.MaxBodyBytes);
        response.Body = capture;
        audit.ResponseCapture = capture;

        // Saved once the response is sent, whoever wrote it: the endpoint or an exception handler placed before
        // this middleware. It also keeps the write to the store out of the time the caller waits.
        response.OnCompleted(() => SaveAsync(context, audit));

        var completed = false;
        try
        {
            await next(context);
            completed = true;
        }
        finally
        {
            audit.Caller = ReadCaller(context.User);

            // After an exception the capture stays in place, so the answer of an exception handler is recorded.
            if (completed)
                response.Body = originalBody;
        }
    }

    private async Task SaveAsync(HttpContext context, PendingAudit audit)
    {
        try
        {
            var response = context.Response;
            using var capture = audit.ResponseCapture!;
            var (responseBody, responseBodyNote) = RequestAuditMasker.MaskBody(
                response.ContentType, capture.Captured, capture.Truncated, _options);

            await store.SaveAsync(new RequestAuditEntry
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = audit.CreatedAtUtc,
                Method = audit.Method,
                Url = audit.Url,
                QueryParams = audit.QueryParams,
                IpAddress = audit.IpAddress,
                RequestHeaders = audit.RequestHeaders,
                RequestBody = audit.RequestBody,
                RequestBodyNote = audit.RequestBodyNote,
                ResponseStatusCode = response.StatusCode,
                ResponseHeaders = RequestAuditMasker.MaskHeaders(response.Headers, _options),
                ResponseBody = responseBody,
                ResponseBodyNote = responseBodyNote,
                RequestId = audit.RequestId,
                TraceParent = audit.TraceParent,
                Caller = audit.Caller,
            });
        }
        catch (Exception ex)
        {
            // Auditing never breaks the request it observes.
            logger.LogError(ex, "The request audit entry could not be saved.");
        }
    }

    private async Task<(string? Body, string? Note)> ReadRequestBodyAsync(HttpRequest request)
    {
        var contentType = request.ContentType;
        var limit = _options.MaxBodyBytes;
        if (request.ContentLength > limit)
            return RequestAuditMasker.MaskBody(contentType, [], truncated: true, _options);

        try
        {
            // Buffered, so the endpoint still reads the whole body after it is rewound.
            request.EnableBuffering();

            // One byte past the limit tells a body of exactly the limit from a larger one.
            var buffer = new byte[(int)Math.Min(request.ContentLength ?? long.MaxValue, limit + 1L)];
            var read = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);
            request.Body.Position = 0;

            return RequestAuditMasker.MaskBody(contentType, buffer[..Math.Min(read, limit)], read > limit, _options);
        }
        catch (Exception ex) when (ex is IOException or BadHttpRequestException)
        {
            // The caller went away or sent a body the server refused: the endpoint will fail on it too.
            return (null, "Not stored: the request body could not be read.");
        }
    }

    private string? ReadIpAddress(HttpContext context) =>
        _options.ClientIpHeader is { Length: > 0 } header && context.Request.Headers.TryGetValue(header, out var forwarded)
            ? forwarded.ToString()
            : context.Connection.RemoteIpAddress?.ToString();

    private Dictionary<string, string> ReadCaller(ClaimsPrincipal user) =>
        _options.CallerClaims
            .Distinct()
            .Select(type => (Type: type, Value: user.FindFirst(type)?.Value))
            .Where(claim => claim.Value is not null)
            .ToDictionary(claim => claim.Type, claim => claim.Value!);

    private sealed class PendingAudit
    {
        public DateTime CreatedAtUtc { get; init; }
        public required string Method { get; init; }
        public required string Url { get; init; }
        public required Dictionary<string, string> QueryParams { get; init; }
        public string? IpAddress { get; init; }
        public required Dictionary<string, string> RequestHeaders { get; init; }
        public required string RequestId { get; init; }
        public string? TraceParent { get; init; }
        public CapturingStream? ResponseCapture { get; set; }
        public Dictionary<string, string> Caller { get; set; } = [];
        public string? RequestBody { get; set; }
        public string? RequestBodyNote { get; set; }
    }
}
