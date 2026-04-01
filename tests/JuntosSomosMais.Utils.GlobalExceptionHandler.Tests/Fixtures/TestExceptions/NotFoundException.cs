namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;

[ExceptionStatusCode(404)]
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
