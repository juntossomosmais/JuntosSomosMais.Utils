namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

/// <summary>Manual clock for the tests that read the time.</summary>
internal sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; private set; } = utcNow;

    public void Advance(TimeSpan by) => UtcNow += by;

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}
