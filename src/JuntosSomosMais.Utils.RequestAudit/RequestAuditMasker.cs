using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Turns what was received and answered into what is safe to store.</summary>
internal static class RequestAuditMasker
{
    internal const string Mask = "***";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Copies the headers, masking the sensitive ones.</summary>
    internal static Dictionary<string, string> MaskHeaders(
        IEnumerable<KeyValuePair<string, StringValues>> headers, RequestAuditOptions options) =>
        MaskValues(headers, options.SensitiveHeaders);

    /// <summary>Copies query parameters or form fields, masking the sensitive ones.</summary>
    internal static Dictionary<string, string> MaskFields(
        IEnumerable<KeyValuePair<string, StringValues>> fields, RequestAuditOptions options) =>
        MaskValues(fields, options.SensitiveFields);

    /// <summary>
    /// The body to store and, when it is not stored, why. Only JSON and URL-encoded forms are stored, because only
    /// those can be masked; a body cut at the size limit cannot be parsed, so it is not stored either.
    /// </summary>
    internal static (string? Body, string? Note) MaskBody(
        string? contentType, byte[] body, bool truncated, RequestAuditOptions options)
    {
        if (truncated)
            return (null, $"Not stored: larger than {options.MaxBodyBytes} bytes.");
        if (body.Length == 0)
            return (null, null);

        return ReadKind(contentType) switch
        {
            BodyKind.Json => MaskJson(body, options),
            BodyKind.Form => (JsonSerializer.Serialize(
                MaskFields(QueryHelpers.ParseQuery(Encoding.UTF8.GetString(body)), options), JsonOptions), null),
            _ => (null, $"Not stored: content type '{contentType}' cannot be masked ({body.Length} bytes)."),
        };
    }

    private static (string? Body, string? Note) MaskJson(byte[] body, RequestAuditOptions options)
    {
        try
        {
            var node = JsonNode.Parse(body);
            MaskNode(node, options.SensitiveFields);
            return (node?.ToJsonString(JsonOptions) ?? "null", null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // ArgumentException: a duplicated property name, found only when the object is walked.
            return (null, $"Not stored: invalid JSON ({body.Length} bytes).");
        }
    }

    private static void MaskNode(JsonNode? node, ISet<string> sensitiveFields)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                MaskNode(item, sensitiveFields);
        }
        else if (node is JsonObject json)
        {
            foreach (var name in json.Select(property => property.Key).ToList())
            {
                if (sensitiveFields.Contains(name))
                    json[name] = Mask;
                else
                    MaskNode(json[name], sensitiveFields);
            }
        }
    }

    private static Dictionary<string, string> MaskValues(
        IEnumerable<KeyValuePair<string, StringValues>> values, ISet<string> sensitive) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => sensitive.Contains(pair.Key) ? Mask : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

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
