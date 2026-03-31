using FluentAssertions;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomErrorResponseTests
{
    [Fact(DisplayName = "Should create custom error response with constructor")]
    public void NewCustomErrorResponse_ValidObject_ShouldCreateObjectWithConstructor()
    {
        var type = "testType";
        var response = new CustomErrorResponse(type, new CustomError());
        response.Type.Should().Be(type);
        response.Error.Should().NotBeNull();
        response.Error!.Msg.Should().BeNull();
    }

    [Fact(DisplayName = "Should create custom error response via property assignment")]
    public void NewCustomErrorResponse_ValidObject_ShouldCreateObject()
    {
        var type = "testType";
        var customError = new CustomError();
        var response = new CustomErrorResponse { Type = type, Error = customError };
        response.Type.Should().Be(type);
        response.Error.Should().NotBeNull();
        response.Error!.Msg.Should().BeNull();
    }

    [Fact(DisplayName = "Should create custom error with constructor")]
    public void NewCustomError_ValidObject_ShouldCreateObjectWithConstructor()
    {
        var msg = "new test error message";
        var response = new CustomError(msg);
        response.Msg.Should().Be(msg);
    }

    [Fact(DisplayName = "Should create custom error via property assignment")]
    public void NewCustomError_ValidObject_ShouldCreateObject()
    {
        var msg = "new test error message";
        var response = new CustomError { Msg = msg };
        response.Msg.Should().Be(msg);
    }

    [Fact(DisplayName = "Should create custom error detail response with constructor")]
    public void NewCustomErrorDetailResponse_ValidObject_ShouldCreateObjectWithConstructor()
    {
        var type = "testType";
        var response = new CustomErrorDetailResponse(type, new CustomErrorDetail());
        response.Type.Should().Be(type);
        response.Error.Should().NotBeNull();
        response.Error!.Msg.Should().BeNull();
        response.Error.Detail.Should().BeNull();
    }

    [Fact(DisplayName = "Should create custom error detail response via property assignment")]
    public void NewCustomErrorDetailResponse_ValidObject_ShouldCreateObject()
    {
        var type = "testType";
        var customError = new CustomErrorDetail();
        var response = new CustomErrorDetailResponse { Type = type, Error = customError };
        response.Type.Should().Be(type);
        response.Error.Should().NotBeNull();
        response.Error!.Msg.Should().BeNull();
        response.Error.Detail.Should().BeNull();
    }

    [Fact(DisplayName = "Should create custom error detail with constructor")]
    public void NewCustomErrorDetail_ValidObject_ShouldCreateObjectWithConstructor()
    {
        var msg = "new test error message";
        var detail = "detail message";
        var response = new CustomErrorDetail(msg, detail);
        response.Msg.Should().Be(msg);
        response.Detail.Should().Be(detail);
    }

    [Fact(DisplayName = "Should create custom error detail via property assignment")]
    public void NewCustomErrorDetail_ValidObject_ShouldCreateObject()
    {
        var msg = "new test error message";
        var detail = "detail message";
        var response = new CustomErrorDetail { Msg = msg, Detail = detail };
        response.Msg.Should().Be(msg);
        response.Detail.Should().Be(detail);
    }
}
