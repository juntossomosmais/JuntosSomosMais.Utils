using System.Runtime.ExceptionServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    private const string RequestBodyOffNote = "Not stored: StoreRequestBody is off.";
    private const string ResponseBodyOffNote = "Not stored: StoreResponseBody is off.";

    private readonly RequestAuditOptions _options = options.Value;
    private readonly RequestAuditMasker _masker = new(options.Value);

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
            QueryParams = _masker.MaskFields(request.Query),
            IpAddress = ReadIpAddress(context),
            RequestHeaders = _masker.MaskHeaders(request.Headers),
            RequestId = context.TraceIdentifier,
            TraceParent = request.Headers.TryGetValue(TraceParentHeader, out var traceParent) ? traceParent.ToString() : null,
        };

        // Read before the endpoint runs: afterwards the stream may be disposed or replaced.
        (audit.RequestBody, audit.RequestBodyNote) = _options.StoreRequestBody
            ? await ReadRequestBodyAsync(request, context.RequestAborted)
            : (null, RequestBodyOffNote);

        var originalBody = response.Body;
        if (_options.StoreResponseBody)
        {
            var capture = new CapturingStream(originalBody, _options.MaxBodyBytes);
            response.Body = capture;
            audit.ResponseCapture = capture;
        }

        // Saved once the response is sent, whoever wrote it: the endpoint or an exception handler placed before
        // this middleware. On HTTP/1.1 the server reads the next request on the connection only after this save,
        // so it is bounded by SaveTimeout.
        response.OnCompleted(() => SaveAsync(context.Response, audit));

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
            if (completed && audit.ResponseCapture is not null)
                await RestoreResponseBodyAsync(context, originalBody);
        }
    }

    /// <summary>
    /// Flushes the writer wrapping the capture, since restoring the body drops it with whatever the endpoint left
    /// unflushed in it, then puts the original body back. A client that went away needs neither the bytes nor an error.
    /// </summary>
    private static async Task RestoreResponseBodyAsync(HttpContext context, Stream originalBody)
    {
        try
        {
            await context.Response.BodyWriter.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The request was aborted: nothing is left to send.
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private async Task SaveAsync(HttpResponse response, PendingAudit audit)
    {
        try
        {
            using var capture = audit.ResponseCapture;
            var (responseBody, responseBodyNote) = capture is null
                ? (null, ResponseBodyOffNote)
                : _masker.MaskBody(response.ContentType, capture.Captured, capture.Truncated);

            var entry = new RequestAuditEntry
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
                ResponseHeaders = _masker.MaskHeaders(response.Headers),
                ResponseBody = responseBody,
                ResponseBodyNote = responseBodyNote,
                RequestId = audit.RequestId,
                TraceParent = audit.TraceParent,
                Caller = audit.Caller,
            };

            using var timeout = new CancellationTokenSource(_options.SaveTimeout, timeProvider);
            await store.SaveAsync(entry, timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The request audit entry could not be saved.");
        }
    }

    private async Task<(string? Body, string? Note)> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var contentType = request.ContentType;
        var limit = _options.MaxBodyBytes;
        if (request.ContentLength > limit)
            return _masker.MaskBody(contentType, [], truncated: true);
        if (request.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>() is { CanHaveBody: false })
            return (null, null);
        if (!_masker.CanMask(contentType))
            return (null, $"Not stored: content type '{contentType}' cannot be masked.");

        // One byte past the limit tells a body of exactly the limit from a larger one.
        var buffer = new byte[(int)Math.Min(request.ContentLength ?? long.MaxValue, limit + 1L)];
        var read = 0;
        try
        {
            int count;
            while (read < buffer.Length && (count = await request.Body.ReadAsync(buffer.AsMemory(read), cancellationToken)) > 0)
                read += count;
        }
        catch (Exception ex)
        {
            // The endpoint gets the bytes consumed so far and then the same failure, as without the audit.
            request.Body = new ReplayStream(buffer.AsMemory(0, read), request.Body, ExceptionDispatchInfo.Capture(ex));
            return (null, "Not stored: the request body could not be read.");
        }

        request.Body = read <= limit
            ? new MemoryStream(buffer, 0, read, writable: false)
            : new ReplayStream(buffer.AsMemory(0, read), request.Body, failure: null);

        return _masker.MaskBody(contentType, buffer[..Math.Min(read, limit)], read > limit);
    }

    private string? ReadIpAddress(HttpContext context) =>
        _options.ClientIpHeader is { Length: > 0 } header && context.Request.Headers.TryGetValue(header, out var forwarded)
            ? forwarded.ToString()
            : context.Connection.RemoteIpAddress?.ToString();

    private Dictionary<string, string> ReadCaller(ClaimsPrincipal user) =>
        _options.CallerClaims
            .Distinct()
            .Select(type => (Type: type, Value: FindClaim(user, type)?.Value))
            .Where(claim => claim.Value is not null)
            .ToDictionary(claim => claim.Type, claim => claim.Value!);

    // JwtBearer's default MapInboundClaims renames sub to NameIdentifier; the entry keeps the name the token has.
    private static Claim? FindClaim(ClaimsPrincipal user, string type) =>
        user.FindFirst(type)
        ?? (type.Equals("sub", StringComparison.OrdinalIgnoreCase) ? user.FindFirst(ClaimTypes.NameIdentifier) : null);

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
