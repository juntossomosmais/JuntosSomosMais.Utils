using MongoDB.Bson;
using MongoDB.Driver;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class HistoryMapperTests
{
    private static readonly ObjectId EntityId = ObjectId.GenerateNewId();
    private static readonly HistoryMapper Mapper = new(new HistoryOptions("orders").IgnoreFields("CreationDate"));
    private static readonly HistoryMapper FullMapper = new(new HistoryOptions("orders") { StoreFullDocument = true }.IgnoreFields("CreationDate"));
    private static readonly DateTime Timestamp = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static ChangeEvent Event(
        ChangeStreamOperationType operation,
        BsonDocument? before = null,
        BsonDocument? after = null,
        BsonDocument? updatedFields = null,
        IReadOnlyList<string>? removed = null,
        IReadOnlyList<string>? truncatedArrays = null) =>
        new(operation, new BsonObjectId(EntityId), before, after, updatedFields, removed, "token-1", Timestamp, truncatedArrays);

    [Fact]
    public void Map_WhenInsert_ShouldCreatePlusRowWithFullDocumentWithoutId()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" }, { "Total", 10 } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Insert, after: after));

        Assert.NotNull(row);
        Assert.Equal("+", row!.HistoryType);
        Assert.Equal(new BsonObjectId(EntityId), row.EntityId);
        Assert.Equal("token-1", row.ChangeToken);
        Assert.Equal(Timestamp, row.HistoryDate);
        Assert.False(row.Data.Contains("_id"));
        Assert.Equal(new BsonDocument { { "StatusOrder", "NEW" }, { "Total", 10 } }, row.Data);
        Assert.Null(row.RemovedFields);
    }

    [Fact]
    public void Map_WhenReplaceChangesScalar_ShouldCreateDeltaWithNewValueOnly()
    {
        var before = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" }, { "Total", 10 } };
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" }, { "Total", 10 } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.NotNull(row);
        Assert.Equal("~", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" } }, row.Data);
        Assert.Null(row.RemovedFields);
    }

    [Fact]
    public void Map_WhenNestedFieldChanges_ShouldStoreWholeTopLevelField()
    {
        var before = new BsonDocument { { "Payments", new BsonDocument { { "Amount", 1 }, { "Method", "PIX" } } } };
        var after = new BsonDocument { { "Payments", new BsonDocument { { "Amount", 2 }, { "Method", "PIX" } } } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Update, before, after));

        Assert.NotNull(row);
        Assert.Equal(new BsonDocument { { "Payments", new BsonDocument { { "Amount", 2 }, { "Method", "PIX" } } } }, row!.Data);
    }

    [Fact]
    public void Map_WhenFieldRemovedAndAnotherAdded_ShouldReportRemovedAndAdded()
    {
        var before = new BsonDocument { { "A", 1 }, { "Z", 2 }, { "B", 3 } };
        var after = new BsonDocument { { "A", 1 }, { "New", "x" } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.NotNull(row);
        Assert.Equal(new BsonDocument { { "New", "x" } }, row!.Data);
        Assert.Equal<string>(["B", "Z"], row.RemovedFields);
    }

    [Fact]
    public void Map_WhenOnlyCreationDateDiffers_ShouldReturnNull()
    {
        var before = new BsonDocument { { "StatusOrder", "NEW" }, { "CreationDate", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) } };
        var after = new BsonDocument { { "StatusOrder", "NEW" }, { "CreationDate", new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc) } };

        Assert.Null(Mapper.Map(Event(ChangeStreamOperationType.Replace, before, after)));
    }

    [Fact]
    public void Map_WhenReplaceIsIdentical_ShouldReturnNull()
    {
        var doc = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" } };

        Assert.Null(Mapper.Map(Event(ChangeStreamOperationType.Replace, doc, doc.DeepClone().AsBsonDocument)));
    }

    [Fact]
    public void Map_WhenReplaceWithoutBefore_ShouldCreateFullSnapshotRow()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Replace, before: null, after: after));

        Assert.NotNull(row);
        Assert.Equal("=", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" } }, row.Data);
    }

    [Fact]
    public void Map_WhenUpdateWithoutBefore_ShouldBuildDeltaFromUpdateDescription()
    {
        var after = new BsonDocument
        {
            { "_id", EntityId },
            { "StatusOrder", "PAID" },
            { "Products", new BsonArray { new BsonDocument("Status", "OK") } },
        };
        var updated = new BsonDocument { { "Products.0.Status", "OK" }, { "StatusOrder", "PAID" } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Update, null, after, updated));

        Assert.NotNull(row);
        Assert.Equal("~", row!.HistoryType);
        Assert.Equal(new BsonDocument
        {
            { "Products", new BsonArray { new BsonDocument("Status", "OK") } },
            { "StatusOrder", "PAID" },
        }, row.Data);
    }

    [Fact]
    public void Map_WhenUpdateWithoutBefore_ShouldUseTruncatedArraysAndFilterRemovedAgainstAfter()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "Items", new BsonArray { 1 } }, { "Back", "again" } };

        var row = Mapper.Map(Event(
            ChangeStreamOperationType.Update,
            null,
            after,
            updatedFields: new BsonDocument(),
            removed: ["Gone.Child", "Back"],
            truncatedArrays: ["Items"]));

        Assert.NotNull(row);
        Assert.Equal(new BsonDocument { { "Items", new BsonArray { 1 } } }, row!.Data);
        Assert.Equal<string>(["Gone"], row.RemovedFields);
    }

    [Fact]
    public void Map_WhenUpdateWithoutBeforeOnlyTouchesIgnoredFields_ShouldReturnNull()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "CreationDate", DateTime.UtcNow } };

        var row = Mapper.Map(Event(
            ChangeStreamOperationType.Update, null, after, new BsonDocument("CreationDate", DateTime.UtcNow)));

        Assert.Null(row);
    }

    [Fact]
    public void Map_WhenDelete_ShouldCreateMinusRowWithBefore()
    {
        var before = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Delete, before));

        Assert.NotNull(row);
        Assert.Equal("-", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "NEW" } }, row.Data);
    }

    [Fact]
    public void Map_WhenDeleteWithoutBefore_ShouldCreateMinusRowWithEmptyData()
    {
        var row = Mapper.Map(Event(ChangeStreamOperationType.Delete));

        Assert.NotNull(row);
        Assert.Equal("-", row!.HistoryType);
        Assert.Empty(row.Data);
    }

    [Fact]
    public void Map_WhenOperationIsNotHandled_ShouldReturnNull()
    {
        Assert.Null(Mapper.Map(Event(ChangeStreamOperationType.Drop)));
    }

    [Fact]
    public void Map_WithDifferentIgnoredSetPerInstance_ShouldHonourEachSet()
    {
        var before = new BsonDocument { { "A", 1 }, { "B", 1 } };
        var after = new BsonDocument { { "A", 2 }, { "B", 2 } };
        var ignoringA = new HistoryMapper(new HistoryOptions("orders").IgnoreFields("A"));
        var ignoringB = new HistoryMapper(new HistoryOptions("orders").IgnoreFields("B"));

        var rowA = ignoringA.Map(Event(ChangeStreamOperationType.Replace, before, after));
        var rowB = ignoringB.Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.Equal(new BsonDocument("B", 2), rowA!.Data);
        Assert.Equal(new BsonDocument("A", 2), rowB!.Data);
    }

    [Fact]
    public void Map_WithEmptyIgnoredSet_ShouldRecordCreationDate()
    {
        var before = new BsonDocument { { "CreationDate", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) } };
        var after = new BsonDocument { { "CreationDate", new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc) } };

        var row = new HistoryMapper(new HistoryOptions("orders")).Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.NotNull(row);
        Assert.True(row!.Data.Contains("CreationDate"));
    }

    [Fact]
    public void Map_WithEmptyIgnoredSet_ShouldAlsoRecordCreationDateWhenUpdateHasNoBefore()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "CreationDate", DateTime.UtcNow } };

        var row = new HistoryMapper(new HistoryOptions("orders")).Map(
            Event(ChangeStreamOperationType.Update, null, after, new BsonDocument("CreationDate", DateTime.UtcNow)));

        Assert.NotNull(row);
        Assert.True(row!.Data.Contains("CreationDate"));
    }

    [Fact]
    public void Constructor_ShouldCopyTheOptionSets()
    {
        var options = new HistoryOptions("orders").IgnoreFields("X").TrackFields("X", "Y");
        var sut = new HistoryMapper(options);
        options.IgnoredFields.Clear();
        options.TrackedFields.Clear();

        var row = sut.Map(Event(ChangeStreamOperationType.Replace, new BsonDocument { { "X", 1 }, { "Z", 1 } }, new BsonDocument { { "X", 2 }, { "Z", 2 } }));

        Assert.Null(row); // X still ignored, Z still untracked
    }

    [Fact]
    public void Map_InDeltaMode_ShouldNotSetChangedFields()
    {
        var before = new BsonDocument { { "StatusOrder", "NEW" } };
        var after = new BsonDocument { { "StatusOrder", "PAID" } };

        var row = Mapper.Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.Null(row!.ChangedFields);
    }

    [Fact]
    public void Map_InFullMode_WhenInsert_ShouldCreatePlusRowWithoutChangedFields()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" }, { "CreationDate", 1 } };

        var row = FullMapper.Map(Event(ChangeStreamOperationType.Insert, after: after));

        Assert.Equal("+", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "NEW" }, { "CreationDate", 1 } }, row.Data);
        Assert.Null(row.ChangedFields);
    }

    [Fact]
    public void Map_InFullMode_WhenReplaceWithBefore_ShouldStoreWholeDocumentAndNameTheChangedFields()
    {
        var before = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" }, { "Total", 10 }, { "Gone", 1 }, { "CreationDate", 1 } };
        var after = new BsonDocument { { "_id", EntityId }, { "Total", 20 }, { "StatusOrder", "PAID" }, { "CreationDate", 2 } };

        var row = FullMapper.Map(Event(ChangeStreamOperationType.Replace, before, after));

        Assert.Equal("~", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "Total", 20 }, { "StatusOrder", "PAID" }, { "CreationDate", 2 } }, row.Data);
        Assert.Equal<string>(["StatusOrder", "Total"], row.ChangedFields);
        Assert.Equal<string>(["Gone"], row.RemovedFields);
    }

    [Fact]
    public void Map_InFullMode_WhenOnlyIgnoredFieldChanges_ShouldReturnNull()
    {
        var before = new BsonDocument { { "StatusOrder", "NEW" }, { "CreationDate", 1 } };
        var after = new BsonDocument { { "StatusOrder", "NEW" }, { "CreationDate", 2 } };

        Assert.Null(FullMapper.Map(Event(ChangeStreamOperationType.Replace, before, after)));
    }

    [Fact]
    public void Map_InFullMode_WhenUpdateWithoutBefore_ShouldStoreWholeDocumentAndChangedFieldsFromTheDescription()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" }, { "Total", 10 }, { "Items", new BsonArray { 1 } } };

        var row = FullMapper.Map(Event(
            ChangeStreamOperationType.Update,
            null,
            after,
            updatedFields: new BsonDocument("StatusOrder", "PAID"),
            removed: ["Gone"],
            truncatedArrays: ["Items"]));

        Assert.Equal("~", row!.HistoryType);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" }, { "Total", 10 }, { "Items", new BsonArray { 1 } } }, row.Data);
        Assert.Equal<string>(["Items", "StatusOrder"], row.ChangedFields);
        Assert.Equal<string>(["Gone"], row.RemovedFields);
    }

    [Fact]
    public void Map_InFullMode_WhenUpdateWithoutBeforeOnlyRemovesFields_ShouldStillMarkTheRowAsFullDocument()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" } };

        var row = FullMapper.Map(Event(ChangeStreamOperationType.Update, null, after, new BsonDocument(), removed: ["Gone"]));

        Assert.NotNull(row!.ChangedFields);
        Assert.Empty(row.ChangedFields);
        Assert.Equal<string>(["Gone"], row.RemovedFields);
        Assert.Equal(new BsonDocument("StatusOrder", "PAID"), row.Data);
    }

    [Fact]
    public void Map_InFullMode_WhenUpdateWithoutBeforeTouchesOnlyIgnoredFields_ShouldReturnNull()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" }, { "CreationDate", 2 } };

        Assert.Null(FullMapper.Map(Event(ChangeStreamOperationType.Update, null, after, new BsonDocument("CreationDate", 2))));
    }

    [Fact]
    public void Map_InFullMode_WhenReplaceWithoutBefore_ShouldCreateSnapshotWithoutChangedFields()
    {
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" } };

        var row = FullMapper.Map(Event(ChangeStreamOperationType.Replace, null, after));

        Assert.Equal("=", row!.HistoryType);
        Assert.Equal(new BsonDocument("StatusOrder", "PAID"), row.Data);
        Assert.Null(row.ChangedFields);
    }

    [Fact]
    public void Map_WithTrackedFields_ShouldDropUntrackedFieldsFromEveryRow()
    {
        var tracked = new HistoryMapper(new HistoryOptions("orders").TrackFields("StatusOrder", "Total"));
        var before = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "NEW" }, { "Total", 10 }, { "Notes", "a" } };
        var after = new BsonDocument { { "_id", EntityId }, { "StatusOrder", "PAID" }, { "Total", 10 }, { "Notes", "b" } };

        var created = tracked.Map(Event(ChangeStreamOperationType.Insert, after: before));
        var changed = tracked.Map(Event(ChangeStreamOperationType.Replace, before, after));
        var deleted = tracked.Map(Event(ChangeStreamOperationType.Delete, before: after));

        Assert.Equal(new BsonDocument { { "StatusOrder", "NEW" }, { "Total", 10 } }, created!.Data);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" } }, changed!.Data);
        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" }, { "Total", 10 } }, deleted!.Data);
    }

    [Fact]
    public void Map_WithTrackedFields_WhenOnlyUntrackedFieldsChange_ShouldReturnNull()
    {
        var tracked = new HistoryMapper(new HistoryOptions("orders").TrackFields("StatusOrder"));
        var before = new BsonDocument { { "StatusOrder", "NEW" }, { "Notes", "a" }, { "Old", 1 } };
        var after = new BsonDocument { { "StatusOrder", "NEW" }, { "Notes", "b" } };

        Assert.Null(tracked.Map(Event(ChangeStreamOperationType.Replace, before, after)));
        Assert.Null(tracked.Map(Event(ChangeStreamOperationType.Update, null, after, new BsonDocument("Notes", "b"), removed: ["Old"])));
    }

    [Fact]
    public void Map_WithTrackedFieldsInFullMode_ShouldStoreOnlyTheTrackedFieldsAndIgnoredOnesNeverCountAsChange()
    {
        var mapper = new HistoryMapper(new HistoryOptions("orders") { StoreFullDocument = true }
            .TrackFields("StatusOrder", "Total", "UpdatedAt")
            .IgnoreFields("UpdatedAt"));
        var before = new BsonDocument { { "StatusOrder", "NEW" }, { "Total", 10 }, { "UpdatedAt", 1 }, { "Notes", "a" } };
        var after = new BsonDocument { { "StatusOrder", "PAID" }, { "Total", 10 }, { "UpdatedAt", 2 }, { "Notes", "b" } };

        var row = mapper.Map(Event(ChangeStreamOperationType.Replace, before, after));
        var onlyIgnored = mapper.Map(Event(ChangeStreamOperationType.Replace, after, new BsonDocument { { "StatusOrder", "PAID" }, { "Total", 10 }, { "UpdatedAt", 3 } }));

        Assert.Equal(new BsonDocument { { "StatusOrder", "PAID" }, { "Total", 10 }, { "UpdatedAt", 2 } }, row!.Data);
        Assert.Equal<string>(["StatusOrder"], row.ChangedFields);
        Assert.Null(onlyIgnored);
    }

    public static TheoryData<BsonValue> EntityIds() => new()
    {
        new BsonString("cart-1"),
        new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
        new BsonInt32(42),
        new BsonObjectId(ObjectId.GenerateNewId()),
    };

    [Theory]
    [MemberData(nameof(EntityIds))]
    public void Map_ShouldPassTheEntityIdThroughUnchanged(BsonValue id)
    {
        var after = new BsonDocument { { "_id", id }, { "A", 1 } };
        var change = new ChangeEvent(ChangeStreamOperationType.Insert, id, null, after, null, null, "t", Timestamp);

        var row = Mapper.Map(change);

        Assert.Equal(id, row!.EntityId);
        Assert.Equal(new BsonDocument("A", 1), row.Data);
    }
}
