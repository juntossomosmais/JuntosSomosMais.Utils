namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;

public class ChildNotFoundException : NotFoundException
{
    public ChildNotFoundException(string message) : base(message) { }
}
