namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class PurgeRequestAuditCommandTests
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IRequestAuditPurger> _purger = new();
    private readonly StringWriter _output = new();

    [Fact]
    public async Task ExecuteAsync_ValidArguments_PurgesAndReportsWhatWasDeleted()
    {
        // Arrange
        _purger.Setup(p => p.PurgeAsync(new RequestAuditPurgeRequest(From, To, false, 5000), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestAuditPurgeResult(12, 12, false, To));

        // Act
        var exitCode = await PurgeRequestAuditCommand.ExecuteAsync(
            ["--from=2026-01-01", "--to=2026-02-01"], () => _purger.Object, _output);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            "Deleted 12 request audit entries from 2026-01-01T00:00:00Z to 2026-02-01T00:00:00Z" + Environment.NewLine,
            _output.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidArguments_ThrowsBeforeCreatingThePurger()
    {
        // Arrange
        var created = false;

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => PurgeRequestAuditCommand.ExecuteAsync(
            ["--bogus"],
            () =>
            {
                created = true;
                return _purger.Object;
            },
            _output));

        // Assert
        Assert.StartsWith("Unrecognized argument: '--bogus'.", exception.Message);
        Assert.False(created);
        Assert.Empty(_output.ToString());
    }

    [Fact]
    public async Task RunAsync_DryRun_ReportsWhatWouldBeDeleted()
    {
        // Arrange
        _purger.Setup(p => p.PurgeAsync(new RequestAuditPurgeRequest(null, To, true, 5000), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestAuditPurgeResult(7, 0, true, To));

        // Act
        var exitCode = await PurgeRequestAuditCommand.RunAsync(
            _purger.Object, new PurgeRequestAuditArgs(null, To, true), _output);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            "7 request audit entries older than 2026-02-01T00:00:00Z would be deleted (dry run)" + Environment.NewLine,
            _output.ToString());
    }

    [Fact]
    public async Task RunAsync_OpenUpperBound_ReportsTheBoundThePurgerApplied()
    {
        // Arrange
        var pinned = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        _purger.Setup(p => p.PurgeAsync(new RequestAuditPurgeRequest(From, null, false, 5000), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestAuditPurgeResult(3, 3, false, pinned));

        // Act
        await PurgeRequestAuditCommand.RunAsync(_purger.Object, new PurgeRequestAuditArgs(From, null, false), _output);

        // Assert
        Assert.Equal(
            "Deleted 3 request audit entries from 2026-01-01T00:00:00Z to 2026-11-10T12:00:00Z" + Environment.NewLine,
            _output.ToString());
    }
}
