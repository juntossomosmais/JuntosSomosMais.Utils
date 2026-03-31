using FluentAssertions;
using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;
using JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;
using Xunit;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests;

public class CustomExceptionsTests
{
    private const string ErrorMessage = "Custom error message example";
    private const string ExceptionTypeLabel = "NEWEXCEPTION_ERROR";

    #region UnauthorizedException

    [Fact(DisplayName = "Should throw unauthorized exception")]
    public void NewUnauthorizedException_ThrowException_ShouldThrowUnauthorizedException()
    {
        Action action = () => throw new UnauthorizedException();
        action.Should().Throw<UnauthorizedException>();
    }

    [Fact(DisplayName = "Should throw unauthorized exception with message")]
    public void NewUnauthorizedException_ThrowException_ShouldThrowUnauthorizedExceptionWithMessage()
    {
        Action action = () => throw new UnauthorizedException(ErrorMessage);
        action.Should().Throw<UnauthorizedException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw unauthorized exception with message and inner exception")]
    public void NewUnauthorizedException_ThrowException_ShouldThrowUnauthorizedExceptionWithMessageAndInnerException()
    {
        Action action = () => throw new UnauthorizedException(ErrorMessage, new UnauthorizedException());
        action.Should().Throw<UnauthorizedException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<UnauthorizedException>();
    }

    [Fact(DisplayName = "Should throw unauthorized exception with message and exception type")]
    public void NewUnauthorizedException_ThrowException_ShouldThrowUnauthorizedExceptionWithMessageAndExceptionType()
    {
        Action action = () => throw new UnauthorizedException(ErrorMessage, ExceptionTypeLabel);
        action.Should().Throw<UnauthorizedException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw unauthorized exception with message, exception type and inner exception")]
    public void NewUnauthorizedException_ThrowException_ShouldThrowUnauthorizedExceptionWithMessageAndExceptionTypeAndInnerException()
    {
        Action action = () => throw new UnauthorizedException(ErrorMessage, ExceptionTypeLabel, new UnauthorizedException());
        action.Should().Throw<UnauthorizedException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<UnauthorizedException>();
    }

    #endregion

    #region CannotAccessException

    [Fact(DisplayName = "Should throw cannot access exception")]
    public void NewCannotAccessException_ThrowException_ShouldThrowCannotAccessException()
    {
        Action action = () => throw new CannotAccessException();
        action.Should().Throw<CannotAccessException>();
    }

    [Fact(DisplayName = "Should throw cannot access exception with message")]
    public void NewCannotAccessException_ThrowException_ShouldThrowCannotAccessExceptionWithMessage()
    {
        Action action = () => throw new CannotAccessException(ErrorMessage);
        action.Should().Throw<CannotAccessException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw cannot access exception with message and inner exception")]
    public void NewCannotAccessException_ThrowException_ShouldThrowCannotAccessExceptionWithMessageAndInnerException()
    {
        Action action = () => throw new CannotAccessException(ErrorMessage, new CannotAccessException());
        action.Should().Throw<CannotAccessException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<CannotAccessException>();
    }

    [Fact(DisplayName = "Should throw cannot access exception with message and exception type")]
    public void NewCannotAccessException_ThrowException_ShouldThrowCannotAccessExceptionWithMessageAndExceptionType()
    {
        Action action = () => throw new CannotAccessException(ErrorMessage, ExceptionTypeLabel);
        action.Should().Throw<CannotAccessException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw cannot access exception with message, exception type and inner exception")]
    public void NewCannotAccessException_ThrowException_ShouldThrowCannotAccessExceptionWithMessageAndExceptionTypeAndInnerException()
    {
        Action action = () => throw new CannotAccessException(ErrorMessage, ExceptionTypeLabel, new CannotAccessException());
        action.Should().Throw<CannotAccessException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<CannotAccessException>();
    }

    #endregion

    #region NotFoundException

    [Fact(DisplayName = "Should throw not found exception")]
    public void NewNotFoundException_ThrowException_ShouldThrowNotFoundException()
    {
        Action action = () => throw new NotFoundException();
        action.Should().Throw<NotFoundException>();
    }

    [Fact(DisplayName = "Should throw not found exception with message")]
    public void NewNotFoundException_ThrowException_ShouldThrowNotFoundExceptionWithMessage()
    {
        Action action = () => throw new NotFoundException(ErrorMessage);
        action.Should().Throw<NotFoundException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw not found exception with message and inner exception")]
    public void NewNotFoundException_ThrowException_ShouldThrowNotFoundExceptionWithMessageAndInnerException()
    {
        Action action = () => throw new NotFoundException(ErrorMessage, new NotFoundException());
        action.Should().Throw<NotFoundException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<NotFoundException>();
    }

    [Fact(DisplayName = "Should throw not found exception with message and exception type")]
    public void NewNotFoundException_ThrowException_ShouldThrowNotFoundExceptionWithMessageAndExceptionType()
    {
        Action action = () => throw new NotFoundException(ErrorMessage, ExceptionTypeLabel);
        action.Should().Throw<NotFoundException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw not found exception with message, exception type and inner exception")]
    public void NewNotFoundException_ThrowException_ShouldThrowNotFoundExceptionWithMessageAndExceptionTypeAndInnerException()
    {
        Action action = () => throw new NotFoundException(ErrorMessage, ExceptionTypeLabel, new NotFoundException());
        action.Should().Throw<NotFoundException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<NotFoundException>();
    }

    #endregion

    #region DomainException / InvalidStateException

    [Fact(DisplayName = "Should throw invalid state exception")]
    public void NewInvalidStateException_ThrowException_ShouldThrowInvalidStateException()
    {
        Action action = () => throw new InvalidStateException();
        action.Should().Throw<InvalidStateException>();
    }

    [Fact(DisplayName = "Should throw invalid state exception with message")]
    public void NewInvalidStateException_ThrowException_ShouldThrowInvalidStateExceptionWithMessage()
    {
        Action action = () => throw new InvalidStateException(ErrorMessage);
        action.Should().Throw<InvalidStateException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw invalid state exception with message and inner exception")]
    public void NewInvalidStateException_ThrowException_ShouldThrowInvalidStateExceptionWithMessageAndInnerException()
    {
        Action action = () => throw new InvalidStateException(ErrorMessage, new InvalidStateException());
        action.Should().Throw<InvalidStateException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<InvalidStateException>();
    }

    [Fact(DisplayName = "Should throw invalid state exception with message and exception type")]
    public void NewInvalidStateException_ThrowException_ShouldThrowInvalidStateExceptionWithMessageAndExceptionType()
    {
        Action action = () => throw new InvalidStateException(ErrorMessage, ExceptionTypeLabel);
        action.Should().Throw<InvalidStateException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw invalid state exception with message, exception type and inner exception")]
    public void NewInvalidStateException_ThrowException_ShouldThrowInvalidStateExceptionWithMessageAndExceptionTypeAndInnerException()
    {
        Action action = () => throw new InvalidStateException(ErrorMessage, ExceptionTypeLabel, new InvalidStateException());
        action.Should().Throw<InvalidStateException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<InvalidStateException>();
    }

    #endregion

    #region DomainException / CustomDomainException

    [Fact(DisplayName = "Should throw custom domain exception")]
    public void NewCustomDomainException_ThrowException_ShouldThrowCustomDomainException()
    {
        Action action = () => throw new CustomDomainException();
        action.Should().Throw<CustomDomainException>();
    }

    [Fact(DisplayName = "Should throw custom domain exception with message")]
    public void NewCustomDomainException_ThrowException_ShouldThrowCustomDomainExceptionWithMessage()
    {
        Action action = () => throw new CustomDomainException(ErrorMessage);
        action.Should().Throw<CustomDomainException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw custom domain exception with message and inner exception")]
    public void NewCustomDomainException_ThrowException_ShouldThrowCustomDomainExceptionWithMessageAndInnerException()
    {
        Action action = () => throw new CustomDomainException(ErrorMessage, new CustomDomainException());
        action.Should().Throw<CustomDomainException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<CustomDomainException>();
    }

    [Fact(DisplayName = "Should throw custom domain exception with message and exception type")]
    public void NewCustomDomainException_ThrowException_ShouldThrowCustomDomainExceptionWithMessageAndExceptionType()
    {
        Action action = () => throw new CustomDomainException(ErrorMessage, ExceptionTypeLabel);
        action.Should().Throw<CustomDomainException>().WithMessage(ErrorMessage);
    }

    [Fact(DisplayName = "Should throw custom domain exception with message, exception type and inner exception")]
    public void NewCustomDomainException_ThrowException_ShouldThrowCustomDomainExceptionWithMessageAndExceptionTypeAndInnerException()
    {
        Action action = () => throw new CustomDomainException(ErrorMessage, ExceptionTypeLabel, new CustomDomainException());
        action.Should().Throw<CustomDomainException>()
            .WithMessage(ErrorMessage)
            .WithInnerException<CustomDomainException>();
    }

    #endregion
}
