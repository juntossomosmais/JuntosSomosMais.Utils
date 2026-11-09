using System.Collections.Concurrent;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public sealed record EndpointProbeEntry(string Path, bool HasEndpointFromContext, bool HasAttributeOnFeatureEndpoint);

public sealed class EndpointProbe
{
    private readonly ConcurrentQueue<EndpointProbeEntry> _entries = new();

    public IReadOnlyList<EndpointProbeEntry> Entries => _entries.ToArray();

    internal void Add(EndpointProbeEntry entry) => _entries.Enqueue(entry);
}

/// <summary>
/// Registered before <see cref="CustomExceptionHandler"/>: records what an <see cref="IExceptionHandler"/> sees
/// when the middleware calls it, and never handles the exception.
/// </summary>
public sealed class EndpointProbeExceptionHandler(EndpointProbe probe) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var feature = httpContext.Features.Get<IExceptionHandlerFeature>();

        probe.Add(new EndpointProbeEntry(
            feature?.Path ?? httpContext.Request.Path,
            httpContext.GetEndpoint() is not null,
            feature?.Endpoint?.Metadata.GetMetadata<IgnoreCustomExceptionAttribute>() is not null));

        return ValueTask.FromResult(false);
    }
}
