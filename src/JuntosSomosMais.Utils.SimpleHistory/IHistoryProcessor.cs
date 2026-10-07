namespace JuntosSomosMais.Utils.SimpleHistory;

/// <summary>Consumes the change stream of ONE tracked collection and records its history rows.</summary>
public interface IHistoryProcessor
{
    /// <summary>
    /// Consumes the change stream and records history rows. Returns when the stream closes or a gap is detected;
    /// cancellation propagates as OperationCanceledException.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken);
}
