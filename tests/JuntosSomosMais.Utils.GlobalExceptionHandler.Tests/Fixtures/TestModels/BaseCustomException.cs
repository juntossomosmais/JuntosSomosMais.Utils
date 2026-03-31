using JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures.TestModels;

public abstract class BaseCustomException : CustomException
{
    protected BaseCustomException() { }
    protected BaseCustomException(string message) : base(message) { }
}

public class ConcreteSubException : BaseCustomException
{
    public ConcreteSubException() { }
    public ConcreteSubException(string message) : base(message) { }
}
