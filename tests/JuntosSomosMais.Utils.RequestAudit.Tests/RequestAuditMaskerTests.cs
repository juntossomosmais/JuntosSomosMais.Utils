using System.Text;
using Microsoft.Extensions.Primitives;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditMaskerTests
{
    private readonly RequestAuditOptions _options = new();

    private RequestAuditMasker Masker => new(_options);

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
        var masked = Masker.MaskHeaders(headers);

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
        var masked = Masker.MaskHeaders(headers);

        // Assert
        Assert.Equal("***", masked["X-Partner-Secret"]);
    }

    [Fact]
    public void MaskFields_SensitiveField_MasksOnlyItsValue()
    {
        // Arrange
        var query = new Dictionary<string, StringValues> { ["Token"] = "abc", ["page"] = "2", ["authorization"] = "Bearer x" };

        // Act
        var masked = Masker.MaskFields(query);

        // Assert
        Assert.Equal(
            new Dictionary<string, string> { ["Token"] = "***", ["page"] = "2", ["authorization"] = "***" },
            masked);
        Assert.Equal("2", masked["PAGE"]);
    }

    [Fact]
    public void MaskBody_EmptyBody_StoresNothingWithoutNote()
    {
        // Arrange
        var body = Array.Empty<byte>();

        // Act
        var result = Masker.MaskBody("application/json", body, truncated: false);

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
        var result = Masker.MaskBody("application/json", body, truncated: true);

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
        var result = Masker.MaskBody(contentType, body, truncated: false);

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
        var result = Masker.MaskBody("application/json", body, truncated: false);

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
        var result = Masker.MaskBody("application/json", body, truncated: false);

        // Assert
        Assert.Equal((null, $"Not stored: invalid JSON ({body.Length} bytes)."), result);
    }

    [Fact]
    public void MaskBody_UrlEncodedForm_IsStoredAsMaskedJsonObject()
    {
        // Arrange
        var body = Bytes("key1=value+1&password=p%40ss&key2=a&key2=b");

        // Act
        var result = Masker.MaskBody("application/x-www-form-urlencoded; charset=utf-8", body, truncated: false);

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
        var result = Masker.MaskBody(contentType, body, truncated: false);

        // Assert
        Assert.Equal((null, $"Not stored: content type '{contentType}' cannot be masked (15 bytes)."), result);
    }

    [Fact]
    public void MaskHeaders_NameOnlyResemblingASensitiveHeader_IsNotMasked()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues> { ["Request.Authorization"] = "kept", ["X-Request-Id"] = "kept" };

        // Act
        var masked = Masker.MaskHeaders(headers);

        // Assert
        Assert.Equal(new Dictionary<string, string> { ["Request.Authorization"] = "kept", ["X-Request-Id"] = "kept" }, masked);
    }

    [Fact]
    public void MaskHeaders_NameContainingASensitivePart_IsMasked()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>
        {
            ["X_Api_Key"] = "a",
            ["X-Client-Secret"] = "b",
            ["X-CSRF-Token"] = "c",
            ["Accept"] = "application/json",
        };

        // Act
        var masked = Masker.MaskHeaders(headers);

        // Assert
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["X_Api_Key"] = "***",
                ["X-Client-Secret"] = "***",
                ["X-CSRF-Token"] = "***",
                ["Accept"] = "application/json",
            },
            masked);
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("accessToken")]
    [InlineData("AccessToken")]
    [InlineData("ACCESS-TOKEN")]
    [InlineData("clientSecret")]
    [InlineData("refreshToken")]
    [InlineData("Password")]
    public void MaskBody_JsonPropertyInAnotherNamingConvention_IsMasked(string name)
    {
        // Arrange
        var body = Bytes($$"""{"{{name}}":"x","nested":{"{{name}}":"y"},"accessType":"Bearer"}""");

        // Act
        var result = Masker.MaskBody("application/json", body, truncated: false);

        // Assert
        Assert.Equal(($$"""{"{{name}}":"***","nested":{"{{name}}":"***"},"accessType":"Bearer"}""", null), result);
    }

    [Fact]
    public void MaskBody_UrlEncodedFormWithModelBoundKeys_MasksByTheLastSegment()
    {
        // Arrange
        var body = Bytes("Input.Password=p&user%5Bpassword%5D=p&user%5Bname%5D=n&phones%5B0%5D=1&Input.UserName=u");

        // Act
        var result = Masker.MaskBody("application/x-www-form-urlencoded", body, truncated: false);

        // Assert
        Assert.Equal(
            ("""{"Input.Password":"***","user[password]":"***","user[name]":"n","phones[0]":"***","Input.UserName":"u"}""", null),
            result);
    }

    [Fact]
    public void MaskFields_ModelBoundQueryKeys_MasksByTheLastSegment()
    {
        // Arrange
        var query = new Dictionary<string, StringValues>
        {
            ["filter.email"] = "a@b.c",
            ["filter[Email]"] = "a@b.c",
            ["emails[0]"] = "a@b.c",
            ["email[0]"] = "a@b.c",
            ["contact.phone[]"] = "1",
            ["filter.name"] = "n",
            ["accessToken"] = "t",
        };

        // Act
        var masked = Masker.MaskFields(query);

        // Assert
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["filter.email"] = "***",
                ["filter[Email]"] = "***",
                ["emails[0]"] = "***",
                ["email[0]"] = "***",
                ["contact.phone[]"] = "***",
                ["filter.name"] = "n",
                ["accessToken"] = "***",
            },
            masked);
    }

    [Fact]
    public void MaskFields_CustomSensitiveFieldInAnotherNamingConvention_MatchesEveryConvention()
    {
        // Arrange
        _options.SensitiveFields.Add("Card-Number");
        var fields = new Dictionary<string, StringValues>
        {
            ["cardNumber"] = "1",
            ["card_number"] = "2",
            ["Payment.CardNumber"] = "3",
            ["card"] = "4",
        };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["cardNumber"] = "***",
                ["card_number"] = "***",
                ["Payment.CardNumber"] = "***",
                ["card"] = "4",
            },
            masked);
    }

    [Theory]
    [InlineData("newPassword")]
    [InlineData("confirm_password")]
    [InlineData("senhaAtual")]
    [InlineData("apiKey")]
    [InlineData("id_token")]
    [InlineData("tokens")]
    [InlineData("clientSecret")]
    [InlineData("cnpj")]
    [InlineData("telefone")]
    [InlineData("celular")]
    [InlineData("cvv")]
    [InlineData("cpfs")]
    [InlineData("Phones")]
    public void MaskFields_DefaultSensitiveName_IsMasked(string name)
    {
        // Arrange
        var fields = new Dictionary<string, StringValues> { [name] = "value" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal("***", masked[name]);
    }

    [Theory]
    [InlineData("address")]
    [InlineData("status")]
    [InlineData("s")]
    [InlineData("name")]
    public void MaskFields_NameEndingInSWithoutASensitiveSingular_IsKept(string name)
    {
        // Arrange
        var fields = new Dictionary<string, StringValues> { [name] = "value" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal("value", masked[name]);
    }

    [Fact]
    public void MaskFields_CustomSensitiveNameParts_ReplaceTheDefaultsAndIgnoreEmptyParts()
    {
        // Arrange
        _options.SensitiveNameParts.Clear();
        _options.SensitiveNameParts.Add("Pin");
        _options.SensitiveNameParts.Add("-");
        _options.SensitiveNameParts.Add(string.Empty);
        var fields = new Dictionary<string, StringValues> { ["cardPin"] = "1", ["newPassword"] = "2", ["name"] = "3" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal(
            new Dictionary<string, string> { ["cardPin"] = "***", ["newPassword"] = "2", ["name"] = "3" },
            masked);
    }

    [Theory]
    [InlineData("access_token", "accesstoken")]
    [InlineData("Input.Password", "password")]
    [InlineData("user[password]", "password")]
    [InlineData("user[phones][0]", "phones")]
    [InlineData("items[0]", "items")]
    [InlineData("items[2fa]", "2fa")]
    [InlineData("0", "0")]
    [InlineData("", "")]
    public void NormalizeFieldName_Key_KeepsTheLastNamedSegment(string name, string expected)
    {
        // Act
        var normalized = RequestAuditMasker.NormalizeFieldName(name);

        // Assert
        Assert.Equal(expected, normalized);
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text.Trim());
}
