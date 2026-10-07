namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class PurgeHistoryArgsTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 16, 50, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_WithNoArguments_ShouldDefaultToOlderThanOneYearOnAllCollections()
    {
        var result = PurgeHistoryArgs.Parse(Array.Empty<string>(), Now);

        Assert.Equal(Now.AddDays(-365), result.ToUtc);
        Assert.Null(result.FromUtc);
        Assert.Empty(result.Collections);
        Assert.False(result.DryRun);
    }

    [Fact]
    public void Parse_WithOlderThanDays_ShouldSetToUtcToNowMinusThoseDays()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--older-than-days=30" }, Now);

        Assert.Equal(Now.AddDays(-30), result.ToUtc);
        Assert.Null(result.FromUtc);
    }

    [Fact]
    public void Parse_WithFromAndTo_ShouldParseAsUtcAndDateOnlyMeansMidnightUtc()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--from=2025-01-01", "--to=2025-03-01T10:30:00Z" }, Now);

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.FromUtc);
        Assert.Equal(DateTimeKind.Utc, result.FromUtc!.Value.Kind);
        Assert.Equal(new DateTime(2025, 3, 1, 10, 30, 0, DateTimeKind.Utc), result.ToUtc);
        Assert.Equal(DateTimeKind.Utc, result.ToUtc!.Value.Kind);
    }

    [Fact]
    public void Parse_WithOffsetDate_ShouldConvertToUtc()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--to=2025-03-01T10:30:00-03:00" }, Now);

        Assert.Equal(new DateTime(2025, 3, 1, 13, 30, 0, DateTimeKind.Utc), result.ToUtc);
    }

    [Fact]
    public void Parse_WithFromOnly_ShouldLeaveToOpenAndNotApplyTheDefault()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--from=2025-01-01" }, Now);

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.FromUtc);
        Assert.Null(result.ToUtc);
    }

    [Fact]
    public void Parse_WithToOnly_ShouldLeaveFromOpen()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--to=2025-01-01" }, Now);

        Assert.Null(result.FromUtc);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.ToUtc);
    }

    [Fact]
    public void Parse_WithCollections_ShouldSplitAndTrim()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--collections=myOrdersHistory, carts" }, Now);

        Assert.Equal(new[] { "myOrdersHistory", "carts" }, result.Collections);
    }

    [Fact]
    public void Parse_WithDryRun_ShouldSetTheFlag()
    {
        Assert.True(PurgeHistoryArgs.Parse(new[] { "--dry-run" }, Now).DryRun);
    }

    [Fact]
    public void Parse_ShouldMatchArgumentNamesCaseInsensitively()
    {
        var result = PurgeHistoryArgs.Parse(new[] { "--DRY-RUN", "--Older-Than-Days=10", "--COLLECTIONS=a" }, Now);

        Assert.True(result.DryRun);
        Assert.Equal(Now.AddDays(-10), result.ToUtc);
        Assert.Equal(new[] { "a" }, result.Collections);
    }

    [Theory]
    [InlineData("--older-than-days=30", "--from=2025-01-01")]
    [InlineData("--to=2025-01-01", "--older-than-days=30")]
    public void Parse_WhenOlderThanDaysIsCombinedWithFromOrTo_ShouldThrow(string first, string second)
    {
        var ex = Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { first, second }, Now));

        Assert.Contains("--older-than-days", ex.Message);
    }

    [Theory]
    [InlineData("--older-than-days=0")]
    [InlineData("--older-than-days=-3")]
    [InlineData("--older-than-days=abc")]
    [InlineData("--older-than-days=1.5")]
    [InlineData("--older-than-days=99999999")]
    public void Parse_WithInvalidOlderThanDays_ShouldThrow(string arg)
    {
        Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { arg }, Now));
    }

    [Theory]
    [InlineData("--from=not-a-date")]
    [InlineData("--to=2025-13-45")]
    public void Parse_WithInvalidDate_ShouldThrow(string arg)
    {
        Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { arg }, Now));
    }

    [Theory]
    [InlineData("--from=2025-02-01", "--to=2025-01-01")]
    [InlineData("--from=2025-01-01", "--to=2025-01-01")]
    public void Parse_WhenFromIsNotBeforeTo_ShouldThrow(string from, string to)
    {
        Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { from, to }, Now));
    }

    [Theory]
    [InlineData("--dry-run", "--dry-run")]
    [InlineData("--from=2025-01-01", "--from=2025-01-02")]
    [InlineData("--collections=a", "--collections=b")]
    [InlineData("--to=2025-01-01", "--TO=2025-01-02")]
    public void Parse_WithDuplicateArgument_ShouldThrow(string first, string second)
    {
        Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { first, second }, Now));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--dry-run=true")]
    [InlineData("positional")]
    public void Parse_WithUnknownArgument_ShouldThrowNamingIt(string arg)
    {
        var ex = Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { arg }, Now));

        Assert.Contains(arg, ex.Message);
    }

    [Theory]
    [InlineData("--collections=")]
    [InlineData("--from=")]
    [InlineData("--to=  ")]
    [InlineData("--older-than-days=")]
    [InlineData("--collections=a,,b")]
    [InlineData("--collections=a, ")]
    public void Parse_WithEmptyValue_ShouldThrow(string arg)
    {
        Assert.Throws<ArgumentException>(() => PurgeHistoryArgs.Parse(new[] { arg }, Now));
    }
}
