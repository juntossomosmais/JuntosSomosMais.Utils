namespace JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

public class CannotAccessException : CustomException
{
    public CannotAccessException() { }
    public CannotAccessException(string message) : base(message) { }
    public CannotAccessException(string message, Exception innerException) : base(message, innerException) { }
    public CannotAccessException(string message, string exceptionType) : base(message)
    {
        ExceptionType = exceptionType;
    }
    public CannotAccessException(string message, string exceptionType, Exception innerException) : base(message, innerException)
    {
        ExceptionType = exceptionType;
    }
}
