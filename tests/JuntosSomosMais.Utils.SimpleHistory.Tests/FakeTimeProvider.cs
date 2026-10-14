namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

/// <summary>Manual clock for the health check tests.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTime utcNow) => UtcNow = utcNow;

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow += by;

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}
