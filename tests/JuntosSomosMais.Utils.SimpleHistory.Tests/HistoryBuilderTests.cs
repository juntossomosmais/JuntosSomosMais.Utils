namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class HistoryBuilderTests
{
    /// <summary>Minimal provider builder: the neutral options as they are.</summary>
    private sealed class TestBuilder : HistoryBuilder<HistoryOptions>
    {
        protected override HistoryOptions CreateOptions(string sourceName) => new(sourceName);
    }

    private static IReadOnlyList<HistoryOptions> Build(Action<TestBuilder> configure)
    {
        var builder = new TestBuilder();
        configure(builder);
        return builder.Build();
    }

    [Fact]
    public void Build_ShouldReturnTheSourcesInRegistrationOrderWithTheirOwnOptions()
    {
        var sources = Build(h => h
            .Track("orders", o => { o.IgnoreFields("CreationDate"); o.BatchSize = 5; })
            .Track("carts"));

        Assert.Equal<string>(["orders", "carts"], sources.Select(o => o.SourceName));
        Assert.Equal<string>(["ordersHistory", "cartsHistory"], sources.Select(o => o.HistoryName));
        Assert.Contains("CreationDate", sources[0].IgnoredFields);
        Assert.Empty(sources[1].IgnoredFields);
        Assert.Equal(5, sources[0].BatchSize);
        Assert.Equal(100, sources[1].BatchSize);
    }

    [Fact]
    public void Track_ShouldReturnTheBuilder()
    {
        var builder = new TestBuilder();

        Assert.Same(builder, builder.Track("orders"));
    }

    [Fact]
    public void Track_WithDuplicateSource_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h.Track("orders").Track("orders")));
    }

    [Fact]
    public void Track_WithBlankName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new TestBuilder().Track(" "));
    }

    [Fact]
    public void Build_WithoutSources_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(_ => { }));
    }

    [Fact]
    public void Build_WithInvalidOptions_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h.Track("orders", o => o.BatchSize = 0)));
    }

    [Fact]
    public void Build_WhenTwoSourcesShareAHistoryName_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h
            .Track("orders", o => o.HistoryName = "audit")
            .Track("carts", o => o.HistoryName = "audit")));
    }

    [Fact]
    public void Build_WhenAHistoryNameIsAlsoTracked_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h
            .Track("orders", o => o.HistoryName = "carts")
            .Track("carts")));
    }

    [Fact]
    public void Build_WhenACheckpointNameIsAlsoTracked_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h
            .Track("orders", o => o.CheckpointName = "carts")
            .Track("carts")));
    }

    [Fact]
    public void Build_WhenACheckpointNameIsAlsoAHistoryName_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => Build(h => h
            .Track("orders", o => o.CheckpointName = "cartsHistory")
            .Track("carts")));
    }
}
