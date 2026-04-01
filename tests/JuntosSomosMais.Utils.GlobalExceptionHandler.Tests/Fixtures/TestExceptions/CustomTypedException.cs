namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestExceptions;

[ExceptionStatusCode(503, ExceptionType = "MY_CUSTOM_TYPE")]
public class CustomTypedException : Exception
{
    public CustomTypedException(string message) : base(message) { }
}
