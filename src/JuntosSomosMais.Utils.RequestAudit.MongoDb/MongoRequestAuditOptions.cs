namespace JuntosSomosMais.Utils.RequestAudit.MongoDb;

/// <summary>Options of the MongoDB provider of the request auditing.</summary>
public sealed class MongoRequestAuditOptions
{
    /// <summary>Collection holding the entries.</summary>
    public string CollectionName { get; set; } = "requestAuditLogs";

    /// <summary>How long an entry is kept. <c>null</c> keeps it forever.</summary>
    public TimeSpan? Retention { get; set; }

    /// <summary>Throws <see cref="InvalidOperationException"/> when the options are invalid.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CollectionName))
            throw new InvalidOperationException("CollectionName is required.");
        if (Retention <= TimeSpan.Zero)
            throw new InvalidOperationException("Retention must be positive.");
    }
}
