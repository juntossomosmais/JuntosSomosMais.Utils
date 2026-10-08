namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class PurgeHistoryCommandTests
{
    private static readonly DateTime To = new(2025, 10, 5, 16, 50, 0, DateTimeKind.Utc);
    private static readonly DateTime From = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<HistoryOptions> Sources = new[]
    {
        new HistoryOptions("myOrders"),
        new HistoryOptions("carts") { HistoryName = "cartsAudit" },
    };

    private readonly Mock<IHistoryPurger> _purger = new();
    private readonly StringWriter _output = new();
    private IReadOnlyList<HistoryOptions>? _received;
    private HistoryPurgeRequest? _request;

    public PurgeHistoryCommandTests()
    {
        _purger
            .Setup(p => p.PurgeAsync(It.IsAny<IEnumerable<HistoryOptions>>(), It.IsAny<HistoryPurgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<HistoryOptions>, HistoryPurgeRequest, CancellationToken>((s, r, _) =>
            {
                _received = s.ToList();
                _request = r;
            })
            .ReturnsAsync((IEnumerable<HistoryOptions> s, HistoryPurgeRequest r, CancellationToken _) =>
                s.Select((o, i) => r.DryRun
                    ? new HistoryPurgeResult(o.HistoryName, 10 * (i + 1), 0, true)
                    : new HistoryPurgeResult(o.HistoryName, 10 * (i + 1), 10 * (i + 1), false)).ToList());
    }

    private Task<int> RunAsync(PurgeHistoryArgs args) => PurgeHistoryCommand.RunAsync(_purger.Object, Sources, args, _output);

    private static PurgeHistoryArgs Args(string[]? collections = null, DateTime? from = null, DateTime? to = null, bool dryRun = false) =>
        new(collections ?? Array.Empty<string>(), from, to, dryRun);

    [Fact]
    public async Task RunAsync_WithNoSelection_ShouldPassAllSourcesAndReturnZero()
    {
        var exitCode = await RunAsync(Args(to: To));

        Assert.Equal(0, exitCode);
        Assert.Equal<string>(["myOrders", "carts"], _received!.Select(s => s.SourceName));
    }

    [Theory]
    [InlineData("cartsAudit")]
    [InlineData("carts")]
    [InlineData("CARTSAUDIT")]
    [InlineData("Carts")]
    public async Task RunAsync_WithCollectionMatchingHistoryOrSourceName_ShouldSelectOnlyIt(string name)
    {
        await RunAsync(Args(new[] { name }, to: To));

        Assert.Equal("cartsAudit", Assert.Single(_received!).HistoryName);
    }

    [Fact]
    public async Task RunAsync_WithDuplicateSelections_ShouldCollapseThem()
    {
        await RunAsync(Args(["carts", "cartsAudit", "CARTS"], to: To));

        Assert.Single(_received!);
    }

    [Fact]
    public async Task RunAsync_WithUnknownCollection_ShouldThrowListingValidNamesAndNeverCallThePurger()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => RunAsync(Args(["carts", "nope"], to: To)));

        Assert.Contains("nope", ex.Message);
        Assert.Contains("myOrders", ex.Message);
        Assert.Contains("myOrdersHistory", ex.Message);
        Assert.Contains("carts", ex.Message);
        Assert.Contains("cartsAudit", ex.Message);
        _purger.Verify(
            p => p.PurgeAsync(It.IsAny<IEnumerable<HistoryOptions>>(), It.IsAny<HistoryPurgeRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldPassTheParsedIntervalAndDryRunToThePurger()
    {
        await RunAsync(Args(from: From, to: To, dryRun: true));

        Assert.Equal(new HistoryPurgeRequest(From, To, true), _request);
    }

    [Fact]
    public async Task RunAsync_OnDryRun_ShouldReportWouldBeDeletedAndTheTotal()
    {
        await RunAsync(Args(to: To, dryRun: true));

        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal<string>(
            [
                "myOrdersHistory: 10 rows older than 2025-10-05T16:50:00Z would be deleted (dry run)",
                "cartsAudit: 20 rows older than 2025-10-05T16:50:00Z would be deleted (dry run)",
                "Total: 30",
            ],
            lines);
    }

    [Fact]
    public async Task RunAsync_OnRealRun_ShouldReportDeletedAndTheTotal()
    {
        await RunAsync(Args(to: To));

        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal<string>(
            [
                "myOrdersHistory: deleted 10 rows older than 2025-10-05T16:50:00Z",
                "cartsAudit: deleted 20 rows older than 2025-10-05T16:50:00Z",
                "Total: 30",
            ],
            lines);
    }

    [Fact]
    public async Task RunAsync_ShouldDescribeTheIntervalAccordingToItsBounds()
    {
        await RunAsync(Args(["carts"], from: From));
        Assert.Contains("deleted 10 rows from 2025-01-01T00:00:00Z", _output.ToString());

        _output.GetStringBuilder().Clear();
        await RunAsync(Args(["carts"], from: From, to: To));
        Assert.Contains("deleted 10 rows from 2025-01-01T00:00:00Z to 2025-10-05T16:50:00Z", _output.ToString());
    }

    [Fact]
    public async Task RunAsync_WhenThePurgerPinnedTheUpperBound_ShouldReportIt()
    {
        var pinned = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        _purger
            .Setup(p => p.PurgeAsync(It.IsAny<IEnumerable<HistoryOptions>>(), It.IsAny<HistoryPurgeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new HistoryPurgeResult("cartsAudit", 4, 4, false, pinned) });

        await RunAsync(Args(["carts"], from: From));

        Assert.Contains("cartsAudit: deleted 4 rows from 2025-01-01T00:00:00Z to 2026-10-06T12:00:00Z", _output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WithValidArguments_ShouldCreateThePurgerAndRunIt()
    {
        var exitCode = await PurgeHistoryCommand.ExecuteAsync(
            ["--collections=carts", "--to=2025-10-05T16:50:00Z"], Sources, () => _purger.Object, _output);

        Assert.Equal(0, exitCode);
        Assert.Equal("cartsAudit", Assert.Single(_received!).HistoryName);
        Assert.Equal(new HistoryPurgeRequest(null, To, false), _request);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--collections=nope")]
    public async Task ExecuteAsync_WithInvalidInput_ShouldThrowWithoutCreatingThePurger(string arg)
    {
        var created = false;

        await Assert.ThrowsAsync<ArgumentException>(() => PurgeHistoryCommand.ExecuteAsync(
            new[] { arg }, Sources, () =>
            {
                created = true;
                return _purger.Object;
            }, _output));

        Assert.False(created);
    }
}
