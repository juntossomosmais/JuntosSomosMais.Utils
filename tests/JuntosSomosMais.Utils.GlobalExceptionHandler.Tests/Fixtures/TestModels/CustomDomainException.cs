using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

public class CustomDomainException : DomainException
{
    public CustomDomainException() { }
    public CustomDomainException(string message) : base(message) { }
    public CustomDomainException(string message, Exception innerException) : base(message, innerException) { }
    public CustomDomainException(string message, string exceptionType) : base(message, exceptionType) { }
    public CustomDomainException(string message, string exceptionType, Exception innerException) : base(message, exceptionType, innerException) { }
}
