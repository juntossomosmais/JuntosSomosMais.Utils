using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JuntosSomosMais.Utils.RequestAudit.Tests;

public class RequestAuditExtensionsTests
{
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
        Assert.Null(options.ClientIpHeader);
        Assert.Equal(new[] { "sub", "client_id", "azp" }, options.CallerClaims);
        Assert.Equal(
            new[] { "Authorization", "Cookie", "Proxy-Authorization", "Set-Cookie", "X-Api-Key" },
            options.SensitiveHeaders.Order());
        Assert.Equal(
            new[] { "access_token", "client_secret", "cpf", "email", "password", "phone", "refresh_token", "secret", "token" },
            options.SensitiveFields.Order());
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
            options.MaxBodyBytes = 10;
            options.ClientIpHeader = "True-Client-IP";
        });
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value;

        // Assert
        Assert.False(options.Enabled);
        Assert.Equal(10, options.MaxBodyBytes);
        Assert.Equal("True-Client-IP", options.ClientIpHeader);
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddRequestAudit_MaxBodyBytesNotPositive_FailsWhenTheOptionsAreRead(int maxBodyBytes)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRequestAudit(options => options.MaxBodyBytes = maxBodyBytes);
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<RequestAuditOptions>>().Value);

        // Assert
        Assert.Equal("RequestAuditOptions.MaxBodyBytes must be positive.", exception.Message);
    }
}
