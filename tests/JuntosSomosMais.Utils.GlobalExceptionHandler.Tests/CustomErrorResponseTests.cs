using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomErrorResponseTests
{
    [Fact(DisplayName = "Should create custom error response with constructor")]
    public void NewCustomErrorResponse_ValidObject_ShouldCreateObjectWithConstructor()
    {
        // Arrange
        var type = "testType";
        var statusCode = 500;

        // Act
        var response = new CustomErrorResponse(type, statusCode, new CustomError());

        // Assert
        Assert.Equal(type, response.Type);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.NotNull(response.Error);
        Assert.Null(response.Error!.RequestId);
        Assert.Null(response.Error.Msg);
    }

    [Fact(DisplayName = "Should create custom error response via property assignment")]
    public void NewCustomErrorResponse_ValidObject_ShouldCreateObject()
    {
        // Arrange
        var type = "testType";
        var statusCode = 500;
        var customError = new CustomError();

        // Act
        var response = new CustomErrorResponse { Type = type, StatusCode = statusCode, Error = customError };

        // Assert
        Assert.Equal(type, response.Type);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.NotNull(response.Error);
    }

    [Fact(DisplayName = "Should create custom error with constructor")]
    public void NewCustomError_ValidObject_ShouldCreateObjectWithConstructor()
    {
        // Arrange
        var requestId = "abc-123";
        var msg = "new test error message";

        // Act
        var response = new CustomError(requestId, msg);

        // Assert
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(msg, response.Msg);
    }

    [Fact(DisplayName = "Should create custom error via property assignment")]
    public void NewCustomError_ValidObject_ShouldCreateObject()
    {
        // Arrange
        var requestId = "abc-123";
        var msg = "new test error message";

        // Act
        var response = new CustomError { RequestId = requestId, Msg = msg };

        // Assert
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(msg, response.Msg);
    }

    [Fact(DisplayName = "Should create custom error detail response with constructor")]
    public void NewCustomErrorDetailResponse_ValidObject_ShouldCreateObjectWithConstructor()
    {
        // Arrange
        var type = "testType";
        var statusCode = 500;

        // Act
        var response = new CustomErrorDetailResponse(type, statusCode, new CustomErrorDetail());

        // Assert
        Assert.Equal(type, response.Type);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.NotNull(response.Error);
        Assert.Null(response.Error!.RequestId);
        Assert.Null(response.Error.Msg);
        Assert.Null(response.Error.Detail);
    }

    [Fact(DisplayName = "Should create custom error detail response via property assignment")]
    public void NewCustomErrorDetailResponse_ValidObject_ShouldCreateObject()
    {
        // Arrange
        var type = "testType";
        var statusCode = 500;
        var customError = new CustomErrorDetail();

        // Act
        var response = new CustomErrorDetailResponse { Type = type, StatusCode = statusCode, Error = customError };

        // Assert
        Assert.Equal(type, response.Type);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.NotNull(response.Error);
    }

    [Fact(DisplayName = "Should create custom error detail with constructor")]
    public void NewCustomErrorDetail_ValidObject_ShouldCreateObjectWithConstructor()
    {
        // Arrange
        var requestId = "abc-123";
        var msg = "new test error message";
        var detail = "detail message";

        // Act
        var response = new CustomErrorDetail(requestId, msg, detail);

        // Assert
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(msg, response.Msg);
        Assert.Equal(detail, response.Detail);
    }

    [Fact(DisplayName = "Should create custom error detail via property assignment")]
    public void NewCustomErrorDetail_ValidObject_ShouldCreateObject()
    {
        // Arrange
        var requestId = "abc-123";
        var msg = "new test error message";
        var detail = "detail message";

        // Act
        var response = new CustomErrorDetail { RequestId = requestId, Msg = msg, Detail = detail };

        // Assert
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(msg, response.Msg);
        Assert.Equal(detail, response.Detail);
    }
}
