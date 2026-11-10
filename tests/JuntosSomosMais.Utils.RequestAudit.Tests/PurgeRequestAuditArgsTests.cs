namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class PurgeRequestAuditArgsTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_NoArguments_DeletesEntriesOlderThanTheDefault()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var parsed = PurgeRequestAuditArgs.Parse(args, Now);

        // Assert
        Assert.Equal(new PurgeRequestAuditArgs(null, Now.AddDays(-365), false), parsed);
        Assert.Equal(DateTimeKind.Utc, parsed.ToUtc!.Value.Kind);
    }

    [Fact]
    public void Parse_OlderThanDaysAndDryRun_SetsTheUpperBound()
    {
        // Arrange
        var args = new[] { "--OLDER-THAN-DAYS=90", "--dry-run" };

        // Act
        var parsed = PurgeRequestAuditArgs.Parse(args, Now);

        // Assert
        Assert.Equal(new PurgeRequestAuditArgs(null, Now.AddDays(-90), true), parsed);
    }

    [Fact]
    public void Parse_FromAndTo_ReadsTheDatesAsUtc()
    {
        // Arrange
        var args = new[] { "--from=2026-01-01", "--to=2026-02-01T03:00:00-03:00" };

        // Act
        var parsed = PurgeRequestAuditArgs.Parse(args, Now);

        // Assert
        Assert.Equal(
            new PurgeRequestAuditArgs(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 1, 6, 0, 0, DateTimeKind.Utc), false),
            parsed);
        Assert.Equal(DateTimeKind.Utc, parsed.FromUtc!.Value.Kind);
    }

    [Theory]
    [InlineData("2026-02-01T06:00:00Z")]
    [InlineData("2026-02-01T03:00:00-03:00")]
    [InlineData("2026-02-01T06:00:00")]
    public void Parse_DateWithOrWithoutOffset_IsConvertedToUtc(string value)
    {
        // Arrange
        var args = new[] { $"--to={value}" };

        // Act
        var parsed = PurgeRequestAuditArgs.Parse(args, Now);

        // Assert
        Assert.Equal(new DateTime(2026, 2, 1, 6, 0, 0, DateTimeKind.Utc), parsed.ToUtc);
        Assert.Equal(DateTimeKind.Utc, parsed.ToUtc!.Value.Kind);
    }

    [Fact]
    public void Parse_ToAtTheCurrentTime_IsAccepted()
    {
        // Arrange
        var args = new[] { "--to=2026-11-10T12:00:00Z" };

        // Act
        var parsed = PurgeRequestAuditArgs.Parse(args, Now);

        // Assert
        Assert.Equal(new PurgeRequestAuditArgs(null, Now, false), parsed);
    }

    [Theory]
    [InlineData("--from=2026-01-01")]
    [InlineData("--to=2026-01-01")]
    public void Parse_OnlyOneBound_LeavesTheOtherOpen(string arg)
    {
        // Arrange
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var parsed = PurgeRequestAuditArgs.Parse([arg], Now);

        // Assert
        var expected = arg.StartsWith("--from", StringComparison.Ordinal)
            ? new PurgeRequestAuditArgs(date, null, false)
            : new PurgeRequestAuditArgs(null, date, false);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(new[] { "--bogus" }, "Unrecognized argument: '--bogus'. Expected '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.")]
    [InlineData(new[] { "--dry-run=true" }, "Unrecognized argument: '--dry-run=true'. Expected '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.")]
    [InlineData(new[] { "--from" }, "Unrecognized argument: '--from'. Expected '--from=<date>', '--to=<date>', '--older-than-days=<N>' or '--dry-run'.")]
    [InlineData(new[] { "--dry-run", "--DRY-RUN" }, "Duplicate argument: '--DRY-RUN'.")]
    [InlineData(new[] { "--from= " }, "Argument '--from' requires a value.")]
    [InlineData(new[] { "--from=yesterday" }, "Invalid date for argument '--from': 'yesterday'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.")]
    [InlineData(new[] { "--from=01/02/2026" }, "Invalid date for argument '--from': '01/02/2026'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.")]
    [InlineData(new[] { "--to=2026-01-01 10:00" }, "Invalid date for argument '--to': '2026-01-01 10:00'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.")]
    [InlineData(new[] { "--from=2026-11-10T12:00:00Z" }, "'--from' (2026-11-10T12:00:00.0000000Z) must be before the current time (2026-11-10T12:00:00.0000000Z).")]
    [InlineData(new[] { "--to=2026-11-10T12:00:01Z" }, "'--to' (2026-11-10T12:00:01.0000000Z) cannot be after the current time (2026-11-10T12:00:00.0000000Z).")]
    [InlineData(new[] { "--older-than-days=0" }, "Invalid value for argument '--older-than-days': '0'. Expected a positive integer.")]
    [InlineData(new[] { "--older-than-days=-1" }, "Invalid value for argument '--older-than-days': '-1'. Expected a positive integer.")]
    [InlineData(new[] { "--older-than-days=abc" }, "Invalid value for argument '--older-than-days': 'abc'. Expected a positive integer.")]
    [InlineData(new[] { "--older-than-days=2000000000" }, "Value of '--older-than-days' is too large: '2000000000'.")]
    [InlineData(new[] { "--older-than-days=30", "--to=2026-01-01" }, "'--older-than-days' cannot be combined with '--from' or '--to'.")]
    [InlineData(new[] { "--older-than-days=30", "--from=2026-01-01" }, "'--older-than-days' cannot be combined with '--from' or '--to'.")]
    [InlineData(new[] { "--from=2026-02-01", "--to=2026-01-01" }, "'--from' (2026-02-01T00:00:00.0000000Z) must be before '--to' (2026-01-01T00:00:00.0000000Z).")]
    [InlineData(new[] { "--from=2026-01-01", "--to=2026-01-01" }, "'--from' (2026-01-01T00:00:00.0000000Z) must be before '--to' (2026-01-01T00:00:00.0000000Z).")]
    public void Parse_InvalidArguments_Throws(string[] args, string message)
    {
        // Arrange
        var now = Now;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => PurgeRequestAuditArgs.Parse(args, now));

        // Assert
        Assert.Equal(message, exception.Message);
    }
}
