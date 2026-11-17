namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditEntryTests
{
    [Fact]
    public void Constructor_NothingSet_TextIsEmptyAndCollectionsAreEmpty()
    {
        // Arrange
        var entry = new RequestAuditEntry();

        // Act
        var text = (entry.Method, entry.Url, entry.RequestId);

        // Assert
        Assert.Equal((string.Empty, string.Empty, string.Empty), text);
        Assert.Empty(entry.QueryParams);
        Assert.Empty(entry.RequestHeaders);
        Assert.Empty(entry.ResponseHeaders);
        Assert.Empty(entry.Caller);
    }
}
