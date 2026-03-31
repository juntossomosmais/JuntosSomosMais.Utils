using FluentAssertions;
using JuntosSomosMais.Utils.GlobalExceptionHandler;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerExtensionsTests
{
    [Fact(DisplayName = "Should throw argument null exception when services is null")]
    public void AddCustomExceptionHandler_WithNullServices_ShouldThrowArgumentNullException()
    {
        Action act = () =>
        {
            IServiceCollection services = null!;
            services.AddCustomExceptionHandler();
        };
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact(DisplayName = "Should throw argument null exception when configure is null")]
    public void AddCustomExceptionHandler_WithNullConfigure_ShouldThrowArgumentNullException()
    {
        Action act = () =>
        {
            var services = new ServiceCollection();
            services.AddCustomExceptionHandler(null!);
        };
        act.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }
}
