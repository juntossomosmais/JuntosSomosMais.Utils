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
        Assert.Equal(TimeSpan.FromSeconds(5), options.SaveTimeout);
        Assert.Null(options.ClientIpHeader);
        Assert.Equal(new[] { "sub", "client_id", "azp" }, options.CallerClaims);
        Assert.Equal(
            new[]
            {
                "Api-Key", "Authorization", "Cookie", "Proxy-Authorization", "Set-Cookie", "X-Access-Token", "X-Api-Key",
                "X-Auth-Token",
            },
            options.SensitiveHeaders.Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "access_token", "client_secret", "cpf", "email", "password", "phone", "refresh_token", "secret", "token" },
            options.SensitiveFields.Order(StringComparer.Ordinal));
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
