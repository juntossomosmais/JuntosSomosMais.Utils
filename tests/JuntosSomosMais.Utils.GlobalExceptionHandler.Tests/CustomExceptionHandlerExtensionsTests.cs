using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionHandlerExtensionsTests
{
    [Fact(DisplayName = "Should throw argument null exception when services is null")]
    public void AddCustomExceptionHandler_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddCustomExceptionHandler();

        // Assert
        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("services", ex.ParamName);
    }

    [Fact(DisplayName = "Should throw argument null exception when configure is null")]
    public void AddCustomExceptionHandler_WithNullConfigure_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddCustomExceptionHandler(null!);

        // Assert
        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("configure", ex.ParamName);
    }
}
