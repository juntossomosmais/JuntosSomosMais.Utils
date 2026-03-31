using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

public class InvalidStateException : DomainException
{
    public InvalidStateException() { }
    public InvalidStateException(string message) : base(message) { }
    public InvalidStateException(string message, Exception innerException) : base(message, innerException) { }
    public InvalidStateException(string message, string exceptionType) : base(message, exceptionType) { }
    public InvalidStateException(string message, string exceptionType, Exception innerException) : base(message, exceptionType, innerException) { }
}
