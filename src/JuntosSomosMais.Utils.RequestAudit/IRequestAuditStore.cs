namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>Where audit entries are kept. Implemented by a storage provider and registered as a singleton.</summary>
public interface IRequestAuditStore
{
    /// <summary>Stores one entry. The token is cancelled after <see cref="RequestAuditOptions.SaveTimeout"/>.</summary>
    public Task SaveAsync(RequestAuditEntry entry, CancellationToken cancellationToken = default);
}
