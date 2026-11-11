using JuntosSomosMais.Utils.Shared;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class PurgeArgumentsTests
{
    private const string Usage = "Expected something.";

    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ParsedDate = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddAll_EveryCommonArgument_RecordsThem()
    {
        // Arrange
        var arguments = Create();

        // Act
        arguments.AddAll(["--FROM=a", "--to=b", "--Dry-Run"]);

        // Assert
        Assert.Equal(ParsedDate, arguments.From);
        Assert.Equal(ParsedDate, arguments.To);
        Assert.True(arguments.DryRun);
    }

    [Fact]
    public void Add_ExtraArgumentInAnyCase_GivesItsValueToItsHandler()
    {
        // Arrange
        string? seen = null;
        var arguments = new PurgeArguments(
            Usage, _ => ParsedDate, new Dictionary<string, Action<string>> { ["--extra"] = value => seen = value });

        // Act
        arguments.Add("--EXTRA=a=b");

        // Assert
        Assert.Equal("a=b", seen);
    }

    [Theory]
    [InlineData("--unknown=1")]
    [InlineData("--from")]
    [InlineData("--dry-run=true")]
    [InlineData("--extra=1")]
    public void Add_UnrecognizedArgument_ThrowsWithTheUsage(string arg)
    {
        // Arrange
        var arguments = Create();

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.Add(arg));

        // Assert
        Assert.Equal($"Unrecognized argument: '{arg}'. {Usage}", error.Message);
    }

    [Fact]
    public void Add_RepeatedInAnotherCase_ThrowsDuplicate()
    {
        // Arrange
        var arguments = Create();
        arguments.Add("--dry-run");

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.Add("--DRY-RUN"));

        // Assert
        Assert.Equal("Duplicate argument: '--DRY-RUN'.", error.Message);
    }

    [Theory]
    [InlineData("--to=")]
    [InlineData("--to=  ")]
    public void Add_EmptyValue_Throws(string arg)
    {
        // Arrange
        var arguments = Create();

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.Add(arg));

        // Assert
        Assert.Equal("Argument '--to' requires a value.", error.Message);
    }

    [Fact]
    public void Add_DateTheParserRejects_Throws()
    {
        // Arrange
        var arguments = new PurgeArguments(Usage, _ => null);

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.Add("--from=bad"));

        // Assert
        Assert.Equal(
            "Invalid date for argument '--from': 'bad'. Expected format: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ.", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("x")]
    public void Add_InvalidOlderThanDays_Throws(string value)
    {
        // Arrange
        var arguments = Create();

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.Add($"--older-than-days={value}"));

        // Assert
        Assert.Equal(
            $"Invalid value for argument '--older-than-days': '{value}'. Expected a positive integer.", error.Message);
    }

    [Fact]
    public void ResolveTo_NoArgument_SubtractsTheDefaultDays()
    {
        // Arrange
        var arguments = Create();

        // Act
        var to = arguments.ResolveTo(Now, defaultOlderThanDays: 30);

        // Assert
        Assert.Equal(Now.AddDays(-30), to);
    }

    [Fact]
    public void ResolveTo_OlderThanDays_SubtractsThem()
    {
        // Arrange
        var arguments = Create();
        arguments.Add("--older-than-days=7");

        // Act
        var to = arguments.ResolveTo(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), defaultOlderThanDays: 30);

        // Assert
        Assert.Equal(Now.AddDays(-7), to);
        Assert.Equal(DateTimeKind.Utc, to!.Value.Kind);
    }

    [Theory]
    [InlineData("--from=a")]
    [InlineData("--to=a")]
    public void ResolveTo_ExplicitBound_KeepsTheGivenUpperBound(string arg)
    {
        // Arrange
        var arguments = Create();
        arguments.Add(arg);

        // Act
        var to = arguments.ResolveTo(Now, defaultOlderThanDays: 30);

        // Assert
        Assert.Equal(arguments.To, to);
    }

    [Theory]
    [InlineData("--from=a")]
    [InlineData("--to=a")]
    public void ResolveTo_OlderThanDaysWithABound_Throws(string arg)
    {
        // Arrange
        var arguments = Create();
        arguments.AddAll([arg, "--older-than-days=1"]);

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.ResolveTo(Now, defaultOlderThanDays: 30));

        // Assert
        Assert.Equal("'--older-than-days' cannot be combined with '--from' or '--to'.", error.Message);
    }

    [Fact]
    public void ResolveTo_TooManyDays_Throws()
    {
        // Arrange
        var arguments = Create();
        arguments.Add("--older-than-days=2147483647");

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.ResolveTo(Now, defaultOlderThanDays: 30));

        // Assert
        Assert.Equal("Value of '--older-than-days' is too large: '2147483647'.", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EnsureFromBeforeTo_FromNotBeforeTo_Throws(int daysBefore)
    {
        // Arrange
        var arguments = Create();
        arguments.Add("--from=a");
        var to = ParsedDate.AddDays(daysBefore);

        // Act
        var error = Assert.Throws<ArgumentException>(() => arguments.EnsureFromBeforeTo(to));

        // Assert
        Assert.Equal($"'--from' ({ParsedDate:O}) must be before '--to' ({to:O}).", error.Message);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void EnsureFromBeforeTo_FromBeforeToOrAnOpenBound_DoesNotThrow(bool withoutFrom, int? daysAfter)
    {
        // Arrange
        var arguments = Create();
        if (!withoutFrom)
            arguments.Add("--from=a");
        DateTime? to = daysAfter is null ? null : ParsedDate.AddDays(daysAfter.Value);

        // Act
        var error = Record.Exception(() => arguments.EnsureFromBeforeTo(to));

        // Assert
        Assert.Null(error);
    }

    private static PurgeArguments Create() => new(Usage, _ => ParsedDate);
}
