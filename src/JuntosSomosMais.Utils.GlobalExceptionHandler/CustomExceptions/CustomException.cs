namespace JuntosSomosMais.Utils.GlobalExceptionHandler.CustomExceptions;

public abstract class CustomException : Exception
{
    public string? ExceptionType { get; protected set; }

    protected CustomException() { }
    protected CustomException(string message) : base(message) { }
    protected CustomException(string message, Exception innerException) : base(message, innerException) { }
}
