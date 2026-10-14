namespace JuntosSomosMais.Utils.SimpleHistory.Tests;

public class HistoryOptionsTests
{
    [Fact]
    public void Constructor_ShouldApplyDefaults()
    {
        var sut = new HistoryOptions("myOrders");

        Assert.Equal("myOrders", sut.SourceName);
        Assert.Equal("myOrdersHistory", sut.HistoryName);
        Assert.Equal("simpleHistoryCheckpoint", sut.CheckpointName);
        Assert.Equal("myOrders", sut.CheckpointId);
        Assert.Empty(sut.IgnoredFields);
        Assert.Empty(sut.TrackedFields);
        Assert.False(sut.StoreFullDocument);
        Assert.Equal(100, sut.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(5), sut.InitialBackoff);
        Assert.Equal(TimeSpan.FromSeconds(60), sut.MaxBackoff);
        Assert.Equal(TimeSpan.FromMinutes(5), sut.UnhealthyAfter);
        Assert.Equal(TimeSpan.FromDays(1), sut.GapReportedFor);
        sut.Validate();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenSourceIsEmpty_ShouldThrow(string? source)
    {
        Assert.Throws<ArgumentException>(() => new HistoryOptions(source!));
    }

    [Fact]
    public void HistoryName_WhenOverridden_ShouldUseTheOverride()
    {
        var sut = new HistoryOptions("orders") { HistoryName = "orders_audit" };

        Assert.Equal("orders_audit", sut.HistoryName);
        Assert.Equal("orders", sut.CheckpointId);
    }

    [Fact]
    public void IgnoreFields_ShouldBeFluentAndCaseSensitive()
    {
        var sut = new HistoryOptions("orders");

        var returned = sut.IgnoreFields("CreationDate", "UpdatedAt").IgnoreFields("CreationDate");

        Assert.Same(sut, returned);
        Assert.Equal(2, sut.IgnoredFields.Count);
        Assert.Contains("CreationDate", sut.IgnoredFields);
        Assert.DoesNotContain("creationdate", sut.IgnoredFields);
    }

    [Fact]
    public void TrackFields_ShouldBeFluentAndCaseSensitive()
    {
        var sut = new HistoryOptions("orders");

        var returned = sut.TrackFields("StatusOrder", "Total").TrackFields("StatusOrder");

        Assert.Same(sut, returned);
        Assert.Equal(2, sut.TrackedFields.Count);
        Assert.Contains("StatusOrder", sut.TrackedFields);
        Assert.DoesNotContain("statusorder", sut.TrackedFields);
        sut.Validate();
    }

    [Fact]
    public void Validate_WhenAFieldIsBothIgnoredAndTracked_ShouldThrowNamingIt()
    {
        var sut = new HistoryOptions("orders").IgnoreFields("CreationDate").TrackFields("StatusOrder", "CreationDate");

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("CreationDate", ex.Message);
    }

    [Fact]
    public void Validate_WhenIdIsIgnoredOrTracked_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(new HistoryOptions("orders").IgnoreFields("_id").Validate);
        Assert.Throws<InvalidOperationException>(new HistoryOptions("orders").TrackFields("_id").Validate);
    }

    [Fact]
    public void IgnoreFields_ShouldNotLeakBetweenInstances()
    {
        var first = new HistoryOptions("orders").IgnoreFields("A");
        var second = new HistoryOptions("carts");

        Assert.Empty(second.IgnoredFields);
        Assert.Single(first.IgnoredFields);
    }

    [Fact]
    public void Validate_WhenHistoryNameEqualsSource_ShouldThrow()
    {
        var sut = new HistoryOptions("orders") { HistoryName = "orders" };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenHistoryNameIsBlank_ShouldThrow(string name)
    {
        var sut = new HistoryOptions("orders") { HistoryName = name };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Fact]
    public void Validate_WhenCheckpointNameIsBlankOrCollidesWithSourceOrHistory_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(new HistoryOptions("orders") { CheckpointName = " " }.Validate);
        Assert.Throws<InvalidOperationException>(new HistoryOptions("orders") { CheckpointName = "orders" }.Validate);
        Assert.Throws<InvalidOperationException>(new HistoryOptions("orders") { CheckpointName = "ordersHistory" }.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenBatchSizeIsNotPositive_ShouldThrow(int batchSize)
    {
        var sut = new HistoryOptions("orders") { BatchSize = batchSize };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Fact]
    public void Validate_WhenMaxBackoffIsShorterThanInitialBackoff_ShouldThrow()
    {
        var sut = new HistoryOptions("orders") { InitialBackoff = TimeSpan.FromSeconds(10), MaxBackoff = TimeSpan.FromSeconds(1) };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenInitialBackoffIsNotPositive_ShouldThrow(int seconds)
    {
        var sut = new HistoryOptions("o") { InitialBackoff = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenGapReportedForIsNotPositive_ShouldThrow(int seconds)
    {
        var sut = new HistoryOptions("o") { GapReportedFor = TimeSpan.FromSeconds(seconds) };

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("GapReportedFor", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenUnhealthyAfterIsNotPositive_ShouldThrow(int seconds)
    {
        var sut = new HistoryOptions("o") { UnhealthyAfter = TimeSpan.FromSeconds(seconds) };

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("UnhealthyAfter", ex.Message);
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(120, 60)]
    public void Validate_WhenUnhealthyAfterIsNotLongerThanMaxBackoff_ShouldThrow(int maxBackoffSeconds, int unhealthySeconds)
    {
        var sut = new HistoryOptions("o")
        {
            MaxBackoff = TimeSpan.FromSeconds(maxBackoffSeconds),
            UnhealthyAfter = TimeSpan.FromSeconds(unhealthySeconds),
        };

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("MaxBackoff", ex.Message);
    }

    [Fact]
    public void Validate_WhenUnhealthyAfterIsJustLongerThanMaxBackoff_ShouldPass()
    {
        new HistoryOptions("o") { MaxBackoff = TimeSpan.FromSeconds(60), UnhealthyAfter = TimeSpan.FromSeconds(61) }.Validate();
    }

    [Theory]
    [InlineData("Address.City", true)]
    [InlineData("$set", true)]
    [InlineData("Address.City", false)]
    [InlineData("$set", false)]
    public void Validate_WhenAFieldNameIsNotTopLevel_ShouldThrow(string name, bool tracked)
    {
        // Arrange
        var sut = new HistoryOptions("orders");
        if (tracked)
            sut.TrackFields("Status", name);
        else
            sut.IgnoreFields(name);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);

        // Assert
        Assert.Equal($"orders: {name} is not a top-level field name.", ex.Message);
    }

    [Fact]
    public void Validate_WhenFieldNamesAreTopLevel_ShouldPass()
    {
        // Arrange
        var sut = new HistoryOptions("orders").TrackFields("Status", "Address", "price$").IgnoreFields("UpdatedAt");

        // Act
        var ex = Record.Exception(sut.Validate);

        // Assert
        Assert.Null(ex);
    }
}
