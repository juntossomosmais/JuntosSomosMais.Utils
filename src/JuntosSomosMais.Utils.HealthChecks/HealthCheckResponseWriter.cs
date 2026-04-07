using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JuntosSomosMais.Utils.HealthChecks;

public static class HealthCheckResponseWriter
{
    private const string ContentType = "application/json";
    private static readonly byte[] EmptyResponse = [(byte)'{', (byte)'}'];

    private static readonly Lazy<JsonSerializerOptions> SerializerOptions = new(() =>
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new TimeSpanJsonConverter());
        return options;
    });

    public static async Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = ContentType;

        if (report != null)
        {
            var uiReport = HealthCheckReport.CreateFrom(report);
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, uiReport, SerializerOptions.Value, httpContext.RequestAborted);
        }
        else
        {
            await httpContext.Response.BodyWriter.WriteAsync(EmptyResponse);
        }
    }
}

public class HealthCheckReport
{
    public HealthCheckReportStatus Status { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, HealthCheckReportEntry> Entries { get; }

    public HealthCheckReport(Dictionary<string, HealthCheckReportEntry> entries, TimeSpan totalDuration)
    {
        Entries = entries ?? new Dictionary<string, HealthCheckReportEntry>();
        TotalDuration = totalDuration;
    }

    public static HealthCheckReport CreateFrom(HealthReport report)
    {
        var uiReport = new HealthCheckReport(new Dictionary<string, HealthCheckReportEntry>(), report.TotalDuration)
        {
            Status = (HealthCheckReportStatus)report.Status
        };

        foreach (var item in report.Entries)
        {
            var entry = new HealthCheckReportEntry
            {
                Data = item.Value.Data,
                Description = item.Value.Description,
                Duration = item.Value.Duration,
                Status = (HealthCheckReportStatus)item.Value.Status,
                Tags = item.Value.Tags
            };

            if (item.Value.Exception != null)
            {
                entry.Exception = item.Value.Exception.Message;
                entry.Description ??= item.Value.Exception.Message;
            }

            uiReport.Entries.Add(item.Key, entry);
        }

        return uiReport;
    }
}

public enum HealthCheckReportStatus
{
    Unhealthy = 0,
    Degraded = 1,
    Healthy = 2
}

public class HealthCheckReportEntry
{
    public IReadOnlyDictionary<string, object>? Data { get; set; }
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Exception { get; set; }
    public HealthCheckReportStatus Status { get; set; }
    public IEnumerable<string>? Tags { get; set; }
}

internal class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of TimeSpan is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
