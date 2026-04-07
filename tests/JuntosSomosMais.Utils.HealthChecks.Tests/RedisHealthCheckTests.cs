using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace JuntosSomosMais.Utils.HealthChecks.Tests;

public class RedisHealthCheckTests
{
    [Fact(DisplayName = "Should throw ArgumentNullException when URI is null")]
    public void Constructor_NullUri_ShouldThrowArgumentNullException()
    {
        // Arrange
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RedisHealthCheck(null!));
    }

    [Fact(DisplayName = "Should return failure status when server is unreachable")]
    public async Task CheckHealthAsync_UnreachableServer_ShouldReturnFailureStatus()
    {
        // Arrange
        var healthCheck = new RedisHealthCheck(new Uri("redis://localhost:1/0"));
        var registration = new HealthCheckRegistration(
            "redis",
            healthCheck,
            HealthStatus.Unhealthy,
            null);
        var context = new HealthCheckContext { Registration = registration };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    public class BuildConfigurationOptionsTests
    {
        [Fact(DisplayName = "Should parse all components from a full URI")]
        public void BuildConfigurationOptions_FullUri_ShouldParseAllComponents()
        {
            // Arrange
            var uri = new Uri("redis://myuser:mypassword@myhost:6380/2");

            // Act
            var options = RedisHealthCheck.BuildConfigurationOptions(uri);

            // Assert
            var endpoint = Assert.Single(options.EndPoints);
            var dnsEndpoint = Assert.IsType<DnsEndPoint>(endpoint);
            Assert.Equal("myhost", dnsEndpoint.Host);
            Assert.Equal(6380, dnsEndpoint.Port);
            Assert.Equal("myuser", options.User);
            Assert.Equal("mypassword", options.Password);
            Assert.Equal(2, options.DefaultDatabase);
            Assert.Equal(5000, options.ConnectTimeout);
            Assert.True(options.AbortOnConnectFail);
        }

        [Fact(DisplayName = "Should set password when only password is provided in URI")]
        public void BuildConfigurationOptions_PasswordOnly_ShouldSetPassword()
        {
            // Arrange
            var uri = new Uri("redis://secret@localhost:6379/0");

            // Act
            var options = RedisHealthCheck.BuildConfigurationOptions(uri);

            // Assert
            Assert.Equal("secret", options.Password);
            Assert.Null(options.User);
        }

        [Fact(DisplayName = "Should not set default database when no database in URI")]
        public void BuildConfigurationOptions_NoDatabase_ShouldNotSetDefaultDatabase()
        {
            // Arrange
            var uri = new Uri("redis://localhost:6379");

            // Act
            var options = RedisHealthCheck.BuildConfigurationOptions(uri);

            // Assert
            Assert.Null(options.DefaultDatabase);
        }

        [Fact(DisplayName = "Should decode URL-encoded credentials")]
        public void BuildConfigurationOptions_EncodedPassword_ShouldDecodeCredentials()
        {
            // Arrange
            var uri = new Uri("redis://user:p%40ss%3Aword@localhost:6379/0");

            // Act
            var options = RedisHealthCheck.BuildConfigurationOptions(uri);

            // Assert
            Assert.Equal("user", options.User);
            Assert.Equal("p@ss:word", options.Password);
        }

        [Fact(DisplayName = "Should throw ArgumentException when database segment is not a number")]
        public void BuildConfigurationOptions_InvalidDatabase_ShouldThrowArgumentException()
        {
            // Arrange
            var uri = new Uri("redis://localhost:6379/notanumber");

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(
                () => RedisHealthCheck.BuildConfigurationOptions(uri));
            Assert.Contains("notanumber", ex.Message);
        }

        [Fact(DisplayName = "Should use fallback port 6379 when no port specified")]
        public void BuildConfigurationOptions_DefaultPort_ShouldUseFallbackPort()
        {
            // Arrange
            var uri = new Uri("redis://localhost");

            // Act
            var options = RedisHealthCheck.BuildConfigurationOptions(uri);

            // Assert
            var endpoint = Assert.Single(options.EndPoints);
            var dnsEndpoint = Assert.IsType<DnsEndPoint>(endpoint);
            Assert.Equal("localhost", dnsEndpoint.Host);
            Assert.Equal(6379, dnsEndpoint.Port);
        }
    }
}
