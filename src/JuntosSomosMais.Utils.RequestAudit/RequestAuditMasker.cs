using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace JuntosSomosMais.Utils.RequestAudit;

internal sealed class RequestAuditMasker(RequestAuditOptions options)
{
    internal const string Mask = "***";

    /// <summary>How many levels of JSON held in a string value (inside another such string) are parsed and masked.</summary>
    internal const int MaxEmbeddedJsonDepth = 3;

    /// <summary>
    /// How many strings per body that look like JSON but are not are parsed before the rest are masked unparsed.
    /// </summary>
    internal const int MaxFailedEmbeddedJsonParses = 100;

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly char[] KeySeparators = ['.', '[', ']'];

    private readonly HashSet<string> _sensitiveFields =
        options.SensitiveFields.Select(term => NormalizeKey(term)[^1]).ToHashSet(StringComparer.Ordinal);

    private readonly string[] _sensitiveParts =
        options.SensitiveNameParts.Select(term => NormalizeKey(term)[^1]).Where(part => part.Length > 0).Distinct().ToArray();

    internal Dictionary<string, string> MaskHeaders(IEnumerable<KeyValuePair<string, StringValues>> headers) =>
        headers.ToDictionary(
            pair => pair.Key,
            pair => options.SensitiveHeaders.Contains(pair.Key) || NormalizeKey(pair.Key).Any(ContainsSensitivePart)
                ? Mask
                : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, string> MaskFields(IEnumerable<KeyValuePair<string, StringValues>> fields) =>
        fields.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveField(pair.Key) ? Mask : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

    internal static bool CanMask(string? contentType) => ReadKind(contentType) != BodyKind.Other;

    internal (string? Body, string? Note) MaskBody(string? contentType, byte[] body, bool truncated)
    {
        if (truncated)
            return (null, $"Not stored: larger than {options.MaxBodyBytes} bytes.");
        if (body.Length == 0)
            return (null, null);

        return ReadKind(contentType) switch
        {
            BodyKind.Json => MaskJson(body),
            BodyKind.Form => (JsonSerializer.Serialize(
                MaskFields(QueryHelpers.ParseQuery(Encoding.UTF8.GetString(body))), JsonOptions), null),
            _ => (null, $"Not stored: content type '{contentType}' cannot be masked ({body.Length} bytes)."),
        };
    }

    /// <summary>
    /// Every named (not numeric) segment of a key, normalized: without <c>_</c> and <c>-</c>, lower case. The key
    /// itself, normalized, when it has none.
    /// </summary>
    internal static string[] NormalizeKey(string name)
    {
        var segments = name
            .Split(KeySeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !part.All(char.IsAsciiDigit))
            .Select(Normalize)
            .ToArray();

        return segments.Length > 0 ? segments : [Normalize(name)];
    }

    private static string Normalize(string segment)
    {
        var normalized = new StringBuilder(segment.Length);
        foreach (var character in segment.Where(character => character is not ('_' or '-')))
            normalized.Append(char.ToLowerInvariant(character));

        return normalized.ToString();
    }

    private bool IsSensitiveField(string name) => NormalizeKey(name).Any(IsSensitiveSegment);

    private bool IsSensitiveSegment(string normalized) =>
        _sensitiveFields.Contains(normalized)
        || (normalized.EndsWith('s') && _sensitiveFields.Contains(normalized[..^1]))
        || (normalized.EndsWith("es", StringComparison.Ordinal) && _sensitiveFields.Contains(normalized[..^2]))
        || ContainsSensitivePart(normalized);

    private bool ContainsSensitivePart(string normalized) =>
        _sensitiveParts.Any(part => normalized.Contains(part, StringComparison.Ordinal));

    private (string? Body, string? Note) MaskJson(byte[] body)
    {
        try
        {
            var failedParses = 0;
            var node = MaskNode(JsonNode.Parse(body), embeddedDepth: 0, ref failedParses);
            return (node?.ToJsonString(JsonOptions) ?? "null", null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            // Found only when the body is walked: a duplicated property name (ArgumentException) or a string
            // that cannot be decoded, also inside embedded JSON (InvalidOperationException).
            return (null, $"Not stored: invalid JSON ({body.Length} bytes).");
        }
    }

    /// <summary>Masks the node in place and returns it, or returns the node replacing it (a masked embedded JSON).</summary>
    private JsonNode? MaskNode(JsonNode? node, int embeddedDepth, ref int failedParses) => node switch
    {
        JsonArray array => MaskArray(array, embeddedDepth, ref failedParses),
        JsonObject json => MaskObject(json, embeddedDepth, ref failedParses),
        JsonValue value when embeddedDepth < MaxEmbeddedJsonDepth && value.TryGetValue<string>(out var text) =>
            MaskEmbeddedJson(text, embeddedDepth + 1, ref failedParses) is { } maskedText ? JsonValue.Create(maskedText) : value,
        _ => node,
    };

    private JsonArray MaskArray(JsonArray array, int embeddedDepth, ref int failedParses)
    {
        for (var index = 0; index < array.Count; index++)
        {
            var item = array[index];
            var masked = MaskNode(item, embeddedDepth, ref failedParses);
            if (!ReferenceEquals(masked, item))
                array[index] = masked;
        }

        return array;
    }

    private JsonObject MaskObject(JsonObject json, int embeddedDepth, ref int failedParses)
    {
        foreach (var name in json.Select(property => property.Key).ToList())
        {
            if (IsSensitiveField(name))
            {
                json[name] = Mask;
                continue;
            }

            var child = json[name];
            var masked = MaskNode(child, embeddedDepth, ref failedParses);
            if (!ReferenceEquals(masked, child))
                json[name] = masked;
        }

        return json;
    }

    /// <summary>
    /// Masks a string value holding a JSON object or array (common in webhooks) and returns it re-serialized, or
    /// null when it is not such a value, or <see cref="Mask"/> once the failed parses ran out.
    /// </summary>
    private string? MaskEmbeddedJson(string text, int embeddedDepth, ref int failedParses)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] is not ('{' or '['))
            return null;

        if (failedParses >= MaxFailedEmbeddedJsonParses)
            return Mask;

        try
        {
            return MaskNode(JsonNode.Parse(text), embeddedDepth, ref failedParses)?.ToJsonString(JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            failedParses++;
            return null;
        }
    }

    private static BodyKind ReadKind(string? contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            return BodyKind.Other;

        var isJson = mediaType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                     || mediaType.Suffix.Equals("json", StringComparison.OrdinalIgnoreCase);
        var isForm = mediaType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

        return (isJson, isForm) switch
        {
            (true, _) => BodyKind.Json,
            (_, true) => BodyKind.Form,
            _ => BodyKind.Other,
        };
    }

    private enum BodyKind
    {
        Other,
        Json,
        Form,
    }
}
