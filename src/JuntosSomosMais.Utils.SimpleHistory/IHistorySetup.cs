namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Abstraction used by the hosted service so setup can be replaced in tests.</summary>
public interface IHistorySetup
{
    /// <summary>Idempotently prepares the collections and indexes of one tracked collection.</summary>
    Task EnsureSetupAsync(CancellationToken cancellationToken = default);
}
