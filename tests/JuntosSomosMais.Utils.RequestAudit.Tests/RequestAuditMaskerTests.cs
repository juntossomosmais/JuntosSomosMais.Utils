using System.Text;
using System.Text.Json.Nodes;
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
            ["authorization"] = "Bearer x",
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
        var body = Bytes("key1=value+1&password=x&key2=a&key2=b&key3=a%40b");

        // Act
        var result = Masker.MaskBody("application/x-www-form-urlencoded; charset=utf-8", body, truncated: false);

        // Assert
        Assert.Equal(("""{"key1":"value 1","password":"***","key2":"a,b","key3":"a@b"}""", null), result);
    }

    [Theory]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain")]
    [InlineData("not a media type")]
    [InlineData(null)]
    public void MaskBody_ContentTypeThatCannotBeMasked_IsNotStored(string? contentType)
    {
        // Arrange
        var body = Bytes("password=x");

        // Act
        var result = Masker.MaskBody(contentType, body, truncated: false);

        // Assert
        Assert.Equal((null, $"Not stored: content type '{contentType}' cannot be masked (10 bytes)."), result);
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
    [InlineData("senha")]
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
    [InlineData("rg")]
    [InlineData("cep")]
    [InlineData("address")]
    [InlineData("addresses")]
    [InlineData("street")]
    [InlineData("district")]
    [InlineData("bairro")]
    [InlineData("logradouro")]
    [InlineData("whatsapp")]
    [InlineData("cvc")]
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
    [InlineData("class")]
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

    [Theory]
    [InlineData("documento")]
    [InlineData("cpfCnpj")]
    [InlineData("customerDocument")]
    [InlineData("userEmail")]
    [InlineData("emails")]
    [InlineData("phoneNumber")]
    [InlineData("mobilePhone")]
    [InlineData("cardNumber")]
    [InlineData("birthDate")]
    [InlineData("dataNascimento")]
    [InlineData("telefoneComercial")]
    public void MaskFields_FieldContainingASensitivePart_IsMasked(string name)
    {
        // Arrange
        var fields = new Dictionary<string, StringValues> { [name] = "value" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal("***", masked[name]);
    }

    [Theory]
    [InlineData("orgId")]
    [InlineData("cargo")]
    [InlineData("accepted")]
    [InlineData("ipAddress")]
    [InlineData("streetNumber")]
    [InlineData("cvvRequired")]
    [InlineData("name")]
    [InlineData("nome")]
    [InlineData("city")]
    public void MaskFields_ExactOnlyName_DoesNotMatchInsideOtherNames(string name)
    {
        // Arrange
        var fields = new Dictionary<string, StringValues> { [name] = "value" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal("value", masked[name]);
    }

    [Fact]
    public void MaskBody_JsonWithDefaultSensitiveNames_MasksThem()
    {
        // Arrange
        var body = Bytes("""
            {"user":{"senha":"a","newPassword":"b","nome":"Ana"},"auth":{"id_token":"c","apiKey":"d"},"name":"Ana"}
            """);

        // Act
        var result = Masker.MaskBody("application/json", body, truncated: false);

        // Assert
        Assert.Equal(
            ("""{"user":{"senha":"***","newPassword":"***","nome":"Ana"},"auth":{"id_token":"***","apiKey":"***"},"name":"Ana"}""",
                null),
            result);
    }

    [Fact]
    public void MaskBody_StargateCustomerDtoShapedResponse_MasksEveryPersonalField()
    {
        // Arrange
        var body = Bytes("""
            {"Id":42,"Name":"Loja Exemplo","Email":"loja@example.com","Telephone":"1130000000","Cnpj":"00000000000100",
             "InscricaoEstadual":"110042490114","Address":"Rua A, 10","PostalCode":"01000000","District":"Centro",
             "City":"São Paulo","State":"SP","Orders":[{"OrderId":7,"Status":"Paid","Total":10.5}]}
            """);

        // Act
        var result = Masker.MaskBody("application/json; charset=utf-8", body, truncated: false);

        // Assert
        Assert.Equal(
            ("""{"Id":42,"Name":"Loja Exemplo","Email":"***","Telephone":"***","Cnpj":"***","InscricaoEstadual":"***","Address":"***","PostalCode":"***","District":"***","City":"São Paulo","State":"SP","Orders":[{"OrderId":7,"Status":"Paid","Total":10.5}]}""",
                null),
            result);
    }

    [Fact]
    public void MaskBody_ObjectUnderSensitiveName_IsMaskedWhole()
    {
        // Arrange
        var body = Bytes("""{"address":{"street":"Rua A","city":"X"},"documents":[{"type":"rg","number":"1"}],"id":1}""");

        // Act
        var result = Masker.MaskBody("application/json", body, truncated: false);

        // Assert
        Assert.Equal(("""{"address":"***","documents":"***","id":1}""", null), result);
    }

    [Fact]
    public void MaskBody_JsonEmbeddedInAString_IsMaskedAndKeptAsString()
    {
        // Arrange
        var body = new JsonObject
        {
            ["event"] = "order.created",
            ["payload"] = """ {"cpf":"123","order":{"id":7},"items":"[{\"email\":\"a@b.c\"}]"}""",
            ["list"] = new JsonArray("""[{"phone":"1"},2]""", "plain"),
        }.ToJsonString();

        // Act
        var (masked, note) = Masker.MaskBody("application/json", Bytes(body), truncated: false);

        // Assert
        Assert.Null(note);
        var json = JsonNode.Parse(masked!)!;
        Assert.Equal("order.created", json["event"]!.GetValue<string>());
        Assert.Equal("""{"cpf":"***","order":{"id":7},"items":"[{\"email\":\"***\"}]"}""", json["payload"]!.GetValue<string>());
        Assert.Equal("""[{"phone":"***"},2]""", json["list"]![0]!.GetValue<string>());
        Assert.Equal("plain", json["list"]![1]!.GetValue<string>());
    }

    [Fact]
    public void MaskBody_ArrayOfObjectsAndArrays_MasksEveryItemAndKeepsTheShape()
    {
        // Arrange
        const string body = """[{"cpf":"1","id":1},[{"email":"a@b.c"},"{\"phone\":\"2\"}"],null,3]""";

        // Act
        var result = Masker.MaskBody("application/json", Bytes(body), truncated: false);

        // Assert
        Assert.Equal(("""[{"cpf":"***","id":1},[{"email":"***"},"{\"phone\":\"***\"}"],null,3]""", null), result);
    }

    [Theory]
    [InlineData("__Access--Token__", "accesstoken")]
    [InlineData("-_-", "")]
    public void NormalizeFieldName_UnderscoresAndHyphens_AreDropped(string name, string expected)
    {
        // Arrange
        // Act
        var normalized = RequestAuditMasker.NormalizeFieldName(name);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[1,")]
    [InlineData("{\"cpf\":1,\"cpf\":2}")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cpf {\"cpf\":1}")]
    public void MaskBody_InvalidJsonInAString_IsKept(string text)
    {
        // Arrange
        var body = new JsonObject { ["note"] = text }.ToJsonString();

        // Act
        var (masked, note) = Masker.MaskBody("application/json", Bytes(body), truncated: false);

        // Assert
        Assert.Null(note);
        Assert.Equal(text, JsonNode.Parse(masked!)!["note"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(99, """{"cpf":"***"}""")]
    [InlineData(100, "***")]
    public void MaskBody_EmbeddedJsonAfterFailedParses_IsParsedOnlyWithinTheBudget(int failures, string expected)
    {
        // Arrange
        var body = BodyWithFailedParsesThenEmbeddedJson(failures);

        // Act
        var (masked, note) = Masker.MaskBody("application/json", Bytes(body), truncated: false);

        // Assert
        Assert.Null(note);
        var items = JsonNode.Parse(masked!)!.AsArray();
        Assert.Equal(failures + 1, items.Count);
        Assert.Equal(expected, items[^1]!.GetValue<string>());
        Assert.All(items.Take(failures), item => Assert.Equal("{", item!.GetValue<string>()));
    }

    [Fact]
    public void MaskBody_FailedParsesOfAnEarlierBody_DoNotCountAgainstTheNext()
    {
        // Arrange
        var masker = Masker;
        var body = Bytes(BodyWithFailedParsesThenEmbeddedJson(99));
        masker.MaskBody("application/json", Bytes(BodyWithFailedParsesThenEmbeddedJson(100)), truncated: false);

        // Act
        var (masked, _) = masker.MaskBody("application/json", body, truncated: false);

        // Assert
        Assert.Equal("""{"cpf":"***"}""", JsonNode.Parse(masked!)!.AsArray()[^1]!.GetValue<string>());
    }

    [Fact]
    public void MaskBody_JsonNestedInStringsBeyondTheDepthLimit_IsKept()
    {
        // Arrange
        // Level 0 is the body; levels 1 to MaxEmbeddedJsonDepth are masked, the next one is kept as received.
        var levels = RequestAuditMasker.MaxEmbeddedJsonDepth + 1;
        var innermost = """{"cpf":"1"}""";
        var text = innermost;
        for (var level = levels - 1; level >= 0; level--)
            text = new JsonObject { ["cpf"] = "1", ["inner"] = text }.ToJsonString();

        // Act
        var (masked, note) = Masker.MaskBody("application/json", Bytes(text), truncated: false);

        // Assert
        Assert.Null(note);
        var node = JsonNode.Parse(masked!)!;
        for (var level = 0; level < levels; level++)
        {
            Assert.Equal("***", node["cpf"]!.GetValue<string>());
            node = JsonNode.Parse(node["inner"]!.GetValue<string>())!;
        }

        Assert.Equal(innermost, node.ToJsonString());
    }

    [Fact]
    public void MaskFields_CustomSensitiveFieldAdded_IsMasked()
    {
        // Arrange
        _options.SensitiveFields.Add("name");
        _options.SensitiveFields.Add("nome");
        var fields = new Dictionary<string, StringValues> { ["name"] = "Ana", ["customer.nome"] = "Ana", ["nickname"] = "A" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal(
            new Dictionary<string, string> { ["name"] = "***", ["customer.nome"] = "***", ["nickname"] = "A" },
            masked);
    }

    [Fact]
    public void MaskFields_DefaultRemovedFromBothSets_IsNotMasked()
    {
        // Arrange
        _options.SensitiveFields.Remove("email");
        _options.SensitiveNameParts.Remove("email");
        var fields = new Dictionary<string, StringValues> { ["email"] = "a@b.c", ["userEmail"] = "a@b.c", ["cpf"] = "1" };

        // Act
        var masked = Masker.MaskFields(fields);

        // Assert
        Assert.Equal(
            new Dictionary<string, string> { ["email"] = "a@b.c", ["userEmail"] = "a@b.c", ["cpf"] = "***" },
            masked);
    }

    private static string BodyWithFailedParsesThenEmbeddedJson(int failures)
    {
        var items = new JsonArray();
        for (var index = 0; index < failures; index++)
            items.Add("{");

        items.Add("""{"cpf":"1"}""");
        return items.ToJsonString();
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text.Trim());
}
