namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class MongoHistoryOptionsTests
{
    [Fact]
    public void Constructor_ShouldApplyTheMongoDefaultsOnTopOfTheBaseOnes()
    {
        var sut = new MongoHistoryOptions("myOrders");

        Assert.Equal("myOrders", sut.SourceName);
        Assert.Equal("myOrdersHistory", sut.HistoryName);
        Assert.True(sut.CompressHistory);
        Assert.Equal(TimeSpan.FromSeconds(5), sut.MaxAwaitTime);
        sut.Validate();
    }

    [Fact]
    public void IgnoreFields_ShouldKeepTheMongoType()
    {
        var sut = new MongoHistoryOptions("orders");

        var returned = sut.IgnoreFields("CreationDate").IgnoreFields("UpdatedAt");
        returned.CompressHistory = false; // compiles only because the chain keeps the Mongo type

        Assert.Same(sut, returned);
        Assert.Equal(2, sut.IgnoredFields.Count);
        Assert.False(sut.CompressHistory);
    }

    [Fact]
    public void TrackFields_ShouldKeepTheMongoType()
    {
        var sut = new MongoHistoryOptions("orders");

        var returned = sut.TrackFields("StatusOrder").TrackFields("Total");
        returned.StoreFullDocument = true; // compiles only because the chain keeps the Mongo type

        Assert.Same(sut, returned);
        Assert.Equal<string>(["StatusOrder", "Total"], sut.TrackedFields.OrderBy(n => n));
        Assert.True(sut.StoreFullDocument);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void Validate_WhenUnhealthyAfterIsNotLongerThanMaxAwaitTime_ShouldThrow(int maxAwaitSeconds, int unhealthySeconds)
    {
        var sut = new MongoHistoryOptions("orders")
        {
            MaxAwaitTime = TimeSpan.FromSeconds(maxAwaitSeconds),
            UnhealthyAfter = TimeSpan.FromSeconds(unhealthySeconds),
            InitialBackoff = TimeSpan.FromSeconds(1), // keep the MaxBackoff rule satisfied: only MaxAwaitTime fails
            MaxBackoff = TimeSpan.FromSeconds(1),
        };

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("MaxAwaitTime", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenMaxAwaitTimeIsNotPositive_ShouldThrow(int seconds)
    {
        var sut = new MongoHistoryOptions("orders") { MaxAwaitTime = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Fact]
    public void Validate_ShouldStillRunTheBaseChecks()
    {
        var sut = new MongoHistoryOptions("orders") { BatchSize = 0 };

        var ex = Assert.Throws<InvalidOperationException>(sut.Validate);
        Assert.Contains("BatchSize", ex.Message);
    }
}
