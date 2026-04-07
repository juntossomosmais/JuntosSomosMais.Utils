using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class HealthCheckResponseWriterTests
{
    [Fact(DisplayName = "Should write JSON with correct status for a healthy report")]
    public async Task WriteAsync_HealthyReport_ShouldWriteJsonWithCorrectStatus()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["sqlserver"] = new(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(50), null, null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(50));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Equal("application/json", httpContext.Response.ContentType);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("entries", out var entriesElement));
        Assert.True(entriesElement.TryGetProperty("sqlserver", out var sqlEntry));
        Assert.Equal("Healthy", sqlEntry.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Should include exception message for an unhealthy report")]
    public async Task WriteAsync_UnhealthyReportWithException_ShouldIncludeExceptionMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Connection failed");
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["rabbitmq"] = new(HealthStatus.Unhealthy, null, TimeSpan.FromMilliseconds(100), exception, null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(100));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
        var rabbitEntry = root.GetProperty("entries").GetProperty("rabbitmq");
        Assert.Equal("Connection failed", rabbitEntry.GetProperty("exception").GetString());
        Assert.Equal("Connection failed", rabbitEntry.GetProperty("description").GetString());
    }

    [Fact(DisplayName = "Should write degraded status for a degraded report")]
    public async Task WriteAsync_DegradedReport_ShouldWriteDegradedStatus()
    {
        // Arrange
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["cache"] = new(HealthStatus.Degraded, "Cache is slow", TimeSpan.FromMilliseconds(200), null, null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(200));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Degraded", root.GetProperty("status").GetString());
        var cacheEntry = root.GetProperty("entries").GetProperty("cache");
        Assert.Equal("Degraded", cacheEntry.GetProperty("status").GetString());
        Assert.Equal("Cache is slow", cacheEntry.GetProperty("description").GetString());
    }

    [Fact(DisplayName = "Should preserve original description when both description and exception exist")]
    public async Task WriteAsync_EntryWithDescriptionAndException_ShouldPreserveOriginalDescription()
    {
        // Arrange
        var exception = new InvalidOperationException("Timeout expired");
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["sqlserver"] = new(HealthStatus.Unhealthy, "Database unreachable", TimeSpan.FromMilliseconds(5000), exception, null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5000));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var sqlEntry = doc.RootElement.GetProperty("entries").GetProperty("sqlserver");
        Assert.Equal("Database unreachable", sqlEntry.GetProperty("description").GetString());
        Assert.Equal("Timeout expired", sqlEntry.GetProperty("exception").GetString());
    }

    [Fact(DisplayName = "Should serialize tags and data when present")]
    public async Task WriteAsync_EntryWithTagsAndData_ShouldIncludeTagsAndData()
    {
        // Arrange
        var data = new Dictionary<string, object> { ["key"] = "value" };
        var tags = new[] { "crucial", "database" };
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["sqlserver"] = new(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(50), null, data, tags)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(50));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var sqlEntry = doc.RootElement.GetProperty("entries").GetProperty("sqlserver");
        Assert.Equal("value", sqlEntry.GetProperty("data").GetProperty("key").GetString());
        var tagsArray = sqlEntry.GetProperty("tags");
        Assert.Equal(2, tagsArray.GetArrayLength());
        Assert.Equal("crucial", tagsArray[0].GetString());
        Assert.Equal("database", tagsArray[1].GetString());
    }

    [Fact(DisplayName = "Should write empty JSON when report is null")]
    public async Task WriteAsync_NullReport_ShouldWriteEmptyJson()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        // Act
        await HealthCheckResponseWriter.WriteAsync(httpContext, null!);

        // Assert
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Equal("application/json", httpContext.Response.ContentType);
        Assert.Equal("{}", json);
    }
}
