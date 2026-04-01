using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class ExceptionStatusCodeAttributeTests
{
    [Theory(DisplayName = "Should throw ArgumentOutOfRangeException for invalid status codes")]
    [InlineData(99)]
    [InlineData(600)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public void Constructor_InvalidStatusCode_ShouldThrowArgumentOutOfRangeException(int statusCode)
    {
        // Arrange & Act
        var act = () => new ExceptionStatusCodeAttribute(statusCode);

        // Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("statusCode", ex.ParamName);
    }

    [Theory(DisplayName = "Should create attribute for valid boundary status codes")]
    [InlineData(100)]
    [InlineData(599)]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(500)]
    public void Constructor_ValidStatusCode_ShouldCreateAttribute(int statusCode)
    {
        // Arrange & Act
        var attr = new ExceptionStatusCodeAttribute(statusCode);

        // Assert
        Assert.Equal(statusCode, attr.StatusCode);
        Assert.Null(attr.ExceptionType);
    }

    [Fact(DisplayName = "Should allow setting ExceptionType")]
    public void ExceptionType_SetValue_ShouldRetainValue()
    {
        // Arrange & Act
        var attr = new ExceptionStatusCodeAttribute(400) { ExceptionType = "MY_TYPE" };

        // Assert
        Assert.Equal(400, attr.StatusCode);
        Assert.Equal("MY_TYPE", attr.ExceptionType);
    }
}
