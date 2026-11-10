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

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly char[] KeySeparators = ['.', '[', ']'];

    private readonly HashSet<string> _sensitiveFields =
        options.SensitiveFields.Select(NormalizeFieldName).ToHashSet(StringComparer.Ordinal);

    internal Dictionary<string, string> MaskHeaders(IEnumerable<KeyValuePair<string, StringValues>> headers) =>
        headers.ToDictionary(
            pair => pair.Key,
            pair => options.SensitiveHeaders.Contains(pair.Key) ? Mask : pair.Value.ToString(),
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
        foreach (var character in segment)
        {
            if (character is not ('_' or '-'))
                normalized.Append(char.ToLowerInvariant(character));
        }

        return normalized.ToString();
    }

    private bool IsSensitiveField(string name) => _sensitiveFields.Contains(NormalizeFieldName(name));

    private (string? Body, string? Note) MaskJson(byte[] body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            MaskNode(node);
            return (node?.ToJsonString(JsonOptions) ?? "null", null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: a duplicated property name, found only when the object is walked.
            return (null, $"Not stored: invalid JSON ({body.Length} bytes).");
        }
    }

    private void MaskNode(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                MaskNode(item);
        }
        else if (node is JsonObject json)
        {
            foreach (var name in json.Select(property => property.Key).ToList())
            {
                if (IsSensitiveField(name))
                    json[name] = Mask;
                else
                    MaskNode(json[name]);
            }
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
