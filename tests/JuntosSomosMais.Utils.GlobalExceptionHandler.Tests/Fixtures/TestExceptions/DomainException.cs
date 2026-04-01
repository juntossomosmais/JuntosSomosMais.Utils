namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;

[ExceptionStatusCode(400)]
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
