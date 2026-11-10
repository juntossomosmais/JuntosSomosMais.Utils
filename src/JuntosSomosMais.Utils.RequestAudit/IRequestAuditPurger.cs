namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Deletes old audit entries (retention). Implemented by a storage provider.</summary>
public interface IRequestAuditPurger
{
    /// <summary>Deletes, or only counts on a dry run, the entries inside the requested interval, in batches.</summary>
    /// <exception cref="ArgumentException">No bound, an empty or inverted interval, an unspecified DateTimeKind or a non-positive batch size.</exception>
    Task<RequestAuditPurgeResult> PurgeAsync(RequestAuditPurgeRequest request, CancellationToken cancellationToken = default);
}
