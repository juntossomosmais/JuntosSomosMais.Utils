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

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly char[] KeySeparators = ['.', '[', ']'];

    private readonly HashSet<string> _sensitiveFields =
        options.SensitiveFields.Select(NormalizeFieldName).ToHashSet(StringComparer.Ordinal);

    private readonly string[] _sensitiveParts =
        options.SensitiveNameParts.Select(NormalizeFieldName).Where(part => part.Length > 0).Distinct().ToArray();

    internal Dictionary<string, string> MaskHeaders(IEnumerable<KeyValuePair<string, StringValues>> headers) =>
        headers.ToDictionary(
            pair => pair.Key,
            pair => options.SensitiveHeaders.Contains(pair.Key) || ContainsSensitivePart(NormalizeFieldName(pair.Key))
                ? Mask
                : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, string> MaskFields(IEnumerable<KeyValuePair<string, StringValues>> fields) =>
        fields.ToDictionary(
            pair => pair.Key,
            pair => IsSensitiveField(pair.Key) ? Mask : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

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

    internal static string NormalizeFieldName(string name)
    {
        var segment = name
            .Split(KeySeparators, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(part => !part.All(char.IsAsciiDigit)) ?? name;

        var normalized = new StringBuilder(segment.Length);
        foreach (var character in segment.Where(character => character is not ('_' or '-')))
            normalized.Append(char.ToLowerInvariant(character));

        return normalized.ToString();
    }

    private bool IsSensitiveField(string name)
    {
        var normalized = NormalizeFieldName(name);
        return _sensitiveFields.Contains(normalized)
               || (normalized.EndsWith('s') && _sensitiveFields.Contains(normalized[..^1]))
               || (normalized.EndsWith("es", StringComparison.Ordinal) && _sensitiveFields.Contains(normalized[..^2]))
               || ContainsSensitivePart(normalized);
    }

    private bool ContainsSensitivePart(string normalized) =>
        _sensitiveParts.Any(part => normalized.Contains(part, StringComparison.Ordinal));

    private (string? Body, string? Note) MaskJson(byte[] body)
    {
        try
        {
            var node = MaskNode(JsonNode.Parse(body), embeddedDepth: 0);
            return (node?.ToJsonString(JsonOptions) ?? "null", null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: a duplicated property name, found only when the object is walked.
            return (null, $"Not stored: invalid JSON ({body.Length} bytes).");
        }
    }

    /// <summary>Masks the node in place and returns it, or returns the node replacing it (a masked embedded JSON).</summary>
    private JsonNode? MaskNode(JsonNode? node, int embeddedDepth) => node switch
    {
        JsonArray array => MaskArray(array, embeddedDepth),
        JsonObject json => MaskObject(json, embeddedDepth),
        JsonValue value when embeddedDepth < MaxEmbeddedJsonDepth && value.TryGetValue<string>(out var text) =>
            MaskEmbeddedJson(text, embeddedDepth + 1) is { } maskedText ? JsonValue.Create(maskedText) : value,
        _ => node,
    };

    private JsonArray MaskArray(JsonArray array, int embeddedDepth)
    {
        for (var index = 0; index < array.Count; index++)
        {
            var item = array[index];
            var masked = MaskNode(item, embeddedDepth);
            if (!ReferenceEquals(masked, item))
                array[index] = masked;
        }

        return array;
    }

    private JsonObject MaskObject(JsonObject json, int embeddedDepth)
    {
        foreach (var name in json.Select(property => property.Key).ToList())
        {
            if (IsSensitiveField(name))
            {
                json[name] = Mask;
                continue;
            }

            var child = json[name];
            var masked = MaskNode(child, embeddedDepth);
            if (!ReferenceEquals(masked, child))
                json[name] = masked;
        }

        return json;
    }

    /// <summary>
    /// Masks a string value holding a JSON object or array (common in webhooks) and returns it re-serialized, or
    /// null when it is not such a value.
    /// </summary>
    private string? MaskEmbeddedJson(string text, int embeddedDepth)
    {
        var trimmed = text.AsSpan().TrimStart();
        if (trimmed.IsEmpty || trimmed[0] is not ('{' or '['))
            return null;

        try
        {
            return MaskNode(JsonNode.Parse(text), embeddedDepth)?.ToJsonString(JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // Not JSON after all: kept as received.
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
