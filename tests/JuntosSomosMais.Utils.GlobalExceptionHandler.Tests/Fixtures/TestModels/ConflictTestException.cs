using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

public class ConflictTestException : CustomException
{
    public ConflictTestException() { }
    public ConflictTestException(string message) : base(message) { }
}
