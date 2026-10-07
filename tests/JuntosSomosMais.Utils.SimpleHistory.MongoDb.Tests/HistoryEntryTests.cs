using MongoDB.Bson;
using MongoDB.Bson.Serialization;
namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class HistoryEntryTests
{
    [Fact]
    public void Defaults_ShouldBeSafe()
    {
        var sut = new HistoryEntry();

        Assert.NotEqual(ObjectId.Empty, sut.Id);
        Assert.True(sut.EntityId.IsBsonNull);
        Assert.Equal("~", sut.HistoryType);
        Assert.Empty(sut.Data);
        Assert.Null(sut.RemovedFields);
        Assert.Null(sut.ChangedFields);
        Assert.Equal("", sut.ChangeToken);
    }

    [Fact]
    public void Serialization_ShouldRoundTripAndOmitNullRemovedAndChangedFields()
    {
        var entry = new HistoryEntry { EntityId = new BsonString("abc"), Data = new BsonDocument("A", 1), ChangeToken = "t" };

        var bson = entry.ToBsonDocument();
        var back = BsonSerializer.Deserialize<HistoryEntry>(bson);

        Assert.False(bson.Contains("RemovedFields"));
        Assert.False(bson.Contains("ChangedFields"));
        Assert.Equal(new BsonString("abc"), back.EntityId);
        Assert.Equal(entry.Data, back.Data);
    }

    [Fact]
    public void Deserialization_ShouldIgnoreExtraElements()
    {
        var bson = new HistoryEntry().ToBsonDocument();
        bson["Extra"] = 1;

        var exception = Record.Exception(() => BsonSerializer.Deserialize<HistoryEntry>(bson));

        Assert.Null(exception);
    }
}
