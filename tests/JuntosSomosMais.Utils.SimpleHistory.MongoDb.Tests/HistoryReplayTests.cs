
using MongoDB.Bson;

namespace JuntosSomosMais.Utils.SimpleHistory.MongoDb.Tests;

public class HistoryReplayTests
{
    private static HistoryEntry Row(string type, BsonDocument data, params string[] removed) => new()
    {
        HistoryType = type,
        Data = data,
        RemovedFields = removed.Length == 0 ? null : removed.ToList(),
        ChangeToken = Guid.NewGuid().ToString("N"),
    };

    private static HistoryEntry FullRow(BsonDocument data, List<string> changed, params string[] removed) => new()
    {
        HistoryType = "~",
        Data = data,
        ChangedFields = changed,
        RemovedFields = removed.Length == 0 ? null : removed.ToList(),
        ChangeToken = Guid.NewGuid().ToString("N"),
    };

    [Fact]
    public void Rebuild_WhenCreatedThenTwoDeltas_ShouldReturnFinalDocument()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 }, { "B", 1 } }),
            Row("~", new BsonDocument { { "A", 2 } }),
            Row("~", new BsonDocument { { "C", 3 } }),
        };

        var result = HistoryReplay.Rebuild(rows);

        Assert.Equal(new BsonDocument { { "A", 2 }, { "B", 1 }, { "C", 3 } }, result);
    }

    [Fact]
    public void Rebuild_WhenUpToChangeToken_ShouldReturnVersionAtThatRow()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 } }),
            Row("~", new BsonDocument { { "A", 2 } }),
            Row("~", new BsonDocument { { "A", 3 } }),
        };

        var result = HistoryReplay.Rebuild(rows, rows[1].ChangeToken);

        Assert.Equal(new BsonDocument { { "A", 2 } }, result);
    }

    [Fact]
    public void Rebuild_WhenDeltaHasRemovedFields_ShouldRemoveThem()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 }, { "B", 1 } }),
            Row("~", new BsonDocument(), "B"),
        };

        Assert.Equal(new BsonDocument { { "A", 1 } }, HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenSnapshotRow_ShouldResetState()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 }, { "B", 1 } }),
            Row("=", new BsonDocument { { "X", 9 } }),
            Row("~", new BsonDocument { { "Y", 8 } }),
        };

        Assert.Equal(new BsonDocument { { "X", 9 }, { "Y", 8 } }, HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenChainHasNoBaseRow_ShouldThrow()
    {
        var rows = new[] { Row("~", new BsonDocument { { "A", 1 } }) };

        Assert.Throws<InvalidOperationException>(() => HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenChainEndsWithDelete_ShouldReturnStateBeforeDelete()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 } }),
            Row("~", new BsonDocument { { "A", 2 } }),
            Row("-", new BsonDocument { { "A", 2 } }),
        };

        Assert.Equal(new BsonDocument { { "A", 2 } }, HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenDeleteHasNoBaseRowButCarriesTheDocument_ShouldReturnIt()
    {
        var rows = new[] { Row("-", new BsonDocument { { "A", 7 } }) };

        Assert.Equal(new BsonDocument { { "A", 7 } }, HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenOnlyAnEmptyDeleteExists_ShouldThrowNoBaseRow()
    {
        var rows = new[] { Row("-", new BsonDocument()) };

        var ex = Assert.Throws<InvalidOperationException>(() => HistoryReplay.Rebuild(rows));
        Assert.Contains("no base row", ex.Message);
    }

    [Fact]
    public void Rebuild_WhenEntityIsReinsertedAfterDelete_ShouldReturnTheReinsertedDocument()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 } }),
            Row("~", new BsonDocument { { "A", 2 } }),
            Row("-", new BsonDocument { { "A", 2 } }),
            Row("+", new BsonDocument { { "B", 1 } }),
            Row("~", new BsonDocument { { "B", 2 } }),
        };

        Assert.Equal(new BsonDocument { { "B", 2 } }, HistoryReplay.Rebuild(rows));
        Assert.Equal(new BsonDocument { { "A", 2 } }, HistoryReplay.Rebuild(rows, rows[2].ChangeToken)); // up to the delete
    }

    [Fact]
    public void Rebuild_WhenDeltaFollowsDeleteWithoutReinsert_ShouldThrow()
    {
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 } }),
            Row("-", new BsonDocument { { "A", 1 } }),
            Row("~", new BsonDocument { { "A", 99 } }),
        };

        Assert.Throws<InvalidOperationException>(() => HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_WhenRowsAreFullDocuments_ShouldReplaceTheStateInsteadOfMerging()
    {
        // Arrange: D is in neither a later Data nor a RemovedFields, so only replacing the state drops it.
        var rows = new[]
        {
            Row("+", new BsonDocument { { "A", 1 }, { "B", 1 }, { "D", 1 } }),
            FullRow(new BsonDocument { { "A", 2 } }, new List<string> { "A" }, "B"),
            FullRow(new BsonDocument { { "A", 2 }, { "C", 3 } }, new List<string> { "C" }),
        };

        // Act
        var latest = HistoryReplay.Rebuild(rows);
        var second = HistoryReplay.Rebuild(rows, rows[1].ChangeToken);

        // Assert
        Assert.Equal(new BsonDocument { { "A", 2 }, { "C", 3 } }, latest);
        Assert.Equal(new BsonDocument { { "A", 2 } }, second);
    }

    [Fact]
    public void Rebuild_WhenFirstRowIsAFullDocumentDelta_ShouldNotNeedABaseRow()
    {
        var rows = new[] { FullRow(new BsonDocument("A", 5), new List<string>()) };

        Assert.Equal(new BsonDocument("A", 5), HistoryReplay.Rebuild(rows));
    }

    [Fact]
    public void Rebuild_ShouldNotMutateRowData()
    {
        var plus = Row("+", new BsonDocument { { "A", 1 } });
        var rows = new[] { plus, Row("~", new BsonDocument { { "A", 2 } }) };

        HistoryReplay.Rebuild(rows);

        Assert.Equal(1, plus.Data["A"].AsInt32);
    }
}
