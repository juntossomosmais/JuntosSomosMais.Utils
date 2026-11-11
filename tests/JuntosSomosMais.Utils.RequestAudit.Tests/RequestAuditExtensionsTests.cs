using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditExtensionsTests
{
    private static readonly string[] DefaultCallerClaims = ["sub", "client_id", "azp"];

    private static readonly string[] DefaultSensitiveHeaders =
    [
        "Api-Key", "Authorization", "Cookie", "Proxy-Authorization", "Set-Cookie", "X-Access-Token", "X-Api-Key",
        "X-Auth-Token",
    ];

    private static readonly string[] DefaultSensitiveFields =
    [
        "address", "authorization", "bairro", "birthdate", "birthday", "cardnumber", "celular", "cep",
        "cnpj", "cpf", "cpfcnpj", "cvc", "cvv", "datanascimento", "district", "document", "documento",
        "email", "endereco", "inscricaoestadual", "logradouro", "mobile", "phone", "postalcode", "rg",
        "street", "telefone", "telephone", "whatsapp", "zipcode",
    ];

    private static readonly string[] DefaultSensitiveNameParts =
    [
        "apikey", "birth", "cardnumber", "celular", "cnpj", "cpf", "document", "email", "nascimento",
        "password", "phone", "secret", "senha", "telefone", "token",
    ];

    [Fact]
    public void AddRequestAudit_WithoutConfiguration_RegistersTheDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRequestAudit();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value;

        // Assert
        Assert.True(options.Enabled);
        Assert.Equal(32 * 1024, options.MaxBodyBytes);
        Assert.Equal(TimeSpan.FromSeconds(5), options.SaveTimeout);
        Assert.Null(options.ClientIpHeader);
        Assert.True(options.StoreRequestBody);
        Assert.True(options.StoreResponseBody);
        Assert.Equal(DefaultCallerClaims, options.CallerClaims);
        Assert.Equal(DefaultSensitiveHeaders, options.SensitiveHeaders.Order(StringComparer.Ordinal));
        Assert.Equal(DefaultSensitiveFields, options.SensitiveFields.Order(StringComparer.Ordinal));
        Assert.Equal(DefaultSensitiveNameParts, options.SensitiveNameParts.Order(StringComparer.Ordinal));
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddRequestAudit_WithConfiguration_AppliesIt()
    {
        // Arrange
        var services = new ServiceCollection();
        var clock = new Mock<TimeProvider>().Object;
        services.AddSingleton(clock);

        // Act
        services.AddRequestAudit(options =>
        {
            options.Enabled = false;
            options.MaxBodyBytes = RequestAuditOptions.MaxBodyBytesLimit;
            options.SaveTimeout = TimeSpan.FromMilliseconds(200);
            options.ClientIpHeader = "True-Client-IP";
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value;

        // Assert
        Assert.False(options.Enabled);
        Assert.Equal(1024 * 1024, options.MaxBodyBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.SaveTimeout);
        Assert.Equal("True-Client-IP", options.ClientIpHeader);
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData((1024 * 1024) + 1)]
    [InlineData(int.MaxValue)]
    public void AddRequestAudit_MaxBodyBytesOutOfRange_FailsWhenTheOptionsAreRead(int maxBodyBytes)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRequestAudit(options => options.MaxBodyBytes = maxBodyBytes);
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value);

        // Assert
        Assert.Equal("RequestAuditOptions.MaxBodyBytes must be between 1 and 1048576.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddRequestAudit_SaveTimeoutNotPositive_FailsWhenTheOptionsAreRead(int milliseconds)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRequestAudit(options => options.SaveTimeout = TimeSpan.FromMilliseconds(milliseconds));
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value);

        // Assert
        Assert.Equal("RequestAuditOptions.SaveTimeout must be positive.", exception.Message);
    }
}
