using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
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
    public void ElementNames_UnderACamelCaseConvention_ShouldKeepThePascalCaseNamesThePurgerAndReadersUse()
    {
        // Mirrors a consumer that registers camelCase globally. A fresh class map runs the conventions again without
        // touching the cached one, and the pack only applies to HistoryEntry and is removed at the end.
        const string packName = nameof(ElementNames_UnderACamelCaseConvention_ShouldKeepThePascalCaseNamesThePurgerAndReadersUse);
        ConventionRegistry.Register(
            packName, new ConventionPack { new CamelCaseElementNameConvention() }, t => t == typeof(HistoryEntry));
        try
        {
            var map = new BsonClassMap<HistoryEntry>();
            map.AutoMap();
            map.Freeze();

            Assert.Equal(
                ["_id", "EntityId", "HistoryDate", "HistoryType", "Data", "RemovedFields", "ChangedFields", "ChangeToken"],
                map.DeclaredMemberMaps.Select(m => m.ElementName));
        }
        finally
        {
            ConventionRegistry.Remove(packName);
        }
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
