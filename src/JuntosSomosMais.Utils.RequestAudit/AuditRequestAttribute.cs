namespace JuntosSomosMais.Utils.RequestAudit;

/// <summary>
/// Opts an endpoint into request auditing. Put it on a controller (every action) or on one action; on minimal APIs
/// add it as endpoint metadata: <c>.WithMetadata(new AuditRequestAttribute())</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AuditRequestAttribute : Attribute;
