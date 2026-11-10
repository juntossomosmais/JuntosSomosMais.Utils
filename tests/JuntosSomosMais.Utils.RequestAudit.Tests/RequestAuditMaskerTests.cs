using System.Text;
using Microsoft.Extensions.Primitives;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditMaskerTests
{
    private readonly RequestAuditOptions _options = new();

    [Fact]
    public void MaskHeaders_SensitiveHeaderInAnyCase_MasksOnlyItsValue()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>
        {
            ["authorization"] = "Bearer secret",
            ["X-API-KEY"] = "key",
            ["Accept"] = new StringValues(["application/json", "text/plain"]),
        };

        // Act
        var masked = RequestAuditMasker.MaskHeaders(headers, _options);

        // Assert
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["authorization"] = "***",
                ["X-API-KEY"] = "***",
                ["Accept"] = "application/json,text/plain",
            },
            masked);
    }

    [Fact]
    public void MaskHeaders_CustomSensitiveHeader_IsMasked()
    {
        // Arrange
        _options.SensitiveHeaders.Add("X-Partner-Secret");
        var headers = new Dictionary<string, StringValues> { ["x-partner-secret"] = "abc" };

        // Act
        var masked = RequestAuditMasker.MaskHeaders(headers, _options);

        // Assert
        Assert.Equal("***", masked["X-Partner-Secret"]);
    }

    [Fact]
    public void MaskFields_SensitiveField_MasksOnlyItsValue()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { ["Token"] = "abc", ["page"] = "2", ["authorization"] = "kept" };

        // Act
        var masked = RequestAuditMasker.MaskFields(query, _options);

        // Assert
        Assert.Equal(
            new Dictionary<string, string> { ["Token"] = "***", ["page"] = "2", ["authorization"] = "kept" },
            masked);
    }

    [Fact]
    public void MaskBody_EmptyBody_StoresNothingWithoutNote()
    {
        // Arrange
        var body = Array.Empty<byte>();

        // Act
        var result = RequestAuditMasker.MaskBody("application/json", body, truncated: false, _options);

        // Assert
        Assert.Equal((null, null), result);
    }

    [Fact]
    public void MaskBody_TruncatedBody_IsNotStored()
    {
        // Arrange
        _options.MaxBodyBytes = 10;
        var body = Bytes("""{"key":"va""");

        // Act
        var result = RequestAuditMasker.MaskBody("application/json", body, truncated: true, _options);

        // Assert
        Assert.Equal((null, "Not stored: larger than 10 bytes."), result);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("APPLICATION/JSON")]
    [InlineData("application/problem+json")]
    public void MaskBody_Json_MasksSensitivePropertiesAtAnyDepth(string contentType)
    {
        // Arrange
        var body = Bytes("""
            {"key":"value","Password":"p","nested":{"email":"a@b.c","items":[{"cpf":"123","qty":1},[{"token":{"a":1}}]]},
             "name":"São Paulo","phone":null}
            """);

        // Act
        var result = RequestAuditMasker.MaskBody(contentType, body, truncated: false, _options);

        // Assert
        Assert.Equal(
            ("""{"key":"value","Password":"***","nested":{"email":"***","items":[{"cpf":"***","qty":1},[{"token":"***"}]]},"name":"São Paulo","phone":"***"}""",
                null),
            result);
    }

    [Theory]
    [InlineData("[1,2,3]", "[1,2,3]")]
    [InlineData("\"text\"", "\"text\"")]
    [InlineData("null", "null")]
    public void MaskBody_JsonThatIsNotAnObject_IsStoredAsIs(string json, string expected)
    {
        // Arrange
        var body = Bytes(json);

        // Act
        var result = RequestAuditMasker.MaskBody("application/json", body, truncated: false, _options);

        // Assert
        Assert.Equal((expected, null), result);
    }

    [Theory]
    [InlineData("{\"key\":")]
    [InlineData("not json")]
    [InlineData("{\"a\":1,\"a\":2}")]
    public void MaskBody_InvalidJson_IsNotStored(string json)
    {
        // Arrange
        var body = Bytes(json);

        // Act
        var result = RequestAuditMasker.MaskBody("application/json", body, truncated: false, _options);

        // Assert
        Assert.Equal((null, $"Not stored: invalid JSON ({body.Length} bytes)."), result);
    }

    [Fact]
    public void MaskBody_UrlEncodedForm_IsStoredAsMaskedJsonObject()
    {
        // Arrange
        var body = Bytes("key1=value+1&password=p%40ss&key2=a&key2=b");

        // Act
        var result = RequestAuditMasker.MaskBody(
            "application/x-www-form-urlencoded; charset=utf-8", body, truncated: false, _options);

        // Assert
        Assert.Equal(("""{"key1":"value 1","password":"***","key2":"a,b"}""", null), result);
    }

    [Theory]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain")]
    [InlineData("not a media type")]
    [InlineData(null)]
    public void MaskBody_ContentTypeThatCannotBeMasked_IsNotStored(string? contentType)
    {
        // Arrange
        var body = Bytes("password=secret");

        // Act
        var result = RequestAuditMasker.MaskBody(contentType, body, truncated: false, _options);

        // Assert
        Assert.Equal((null, $"Not stored: content type '{contentType}' cannot be masked (15 bytes)."), result);
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text.Trim());
}
