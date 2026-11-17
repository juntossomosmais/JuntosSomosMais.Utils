namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>One audited HTTP request and its response. Sensitive values are already masked.</summary>
public sealed class RequestAuditEntry
{
    /// <summary>Identifier of the entry.</summary>
    public Guid Id { get; init; }

    /// <summary>When the request arrived.</summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>HTTP method.</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Scheme, host and path, without the query string.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Query string parameters.</summary>
    public IReadOnlyDictionary<string, string> QueryParams { get; init; } = new Dictionary<string, string>();

    /// <summary>Address of the caller.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Request headers.</summary>
    public IReadOnlyDictionary<string, string> RequestHeaders { get; init; } = new Dictionary<string, string>();

    /// <summary>Request body as JSON, or <c>null</c> when there was none or it was not stored.</summary>
    public string? RequestBody { get; init; }

    /// <summary>Why the request body was not stored, when it was not.</summary>
    public string? RequestBodyNote { get; init; }

    /// <summary>Status code answered.</summary>
    public int ResponseStatusCode { get; init; }

    /// <summary>Response headers.</summary>
    public IReadOnlyDictionary<string, string> ResponseHeaders { get; init; } = new Dictionary<string, string>();

    /// <summary>Response body as JSON, or <c>null</c> when there was none or it was not stored.</summary>
    public string? ResponseBody { get; init; }

    /// <summary>Why the response body was not stored, when it was not.</summary>
    public string? ResponseBodyNote { get; init; }

    /// <summary>The <c>HttpContext.TraceIdentifier</c> of the request.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>The <c>traceparent</c> header received.</summary>
    public string? TraceParent { get; init; }

    /// <summary>Claims that identify the caller, by claim type.</summary>
    public IReadOnlyDictionary<string, string> Caller { get; init; } = new Dictionary<string, string>();
}
