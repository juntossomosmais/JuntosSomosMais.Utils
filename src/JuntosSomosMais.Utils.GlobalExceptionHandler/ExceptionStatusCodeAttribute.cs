using System;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ExceptionStatusCodeAttribute : Attribute
{
    public int StatusCode { get; }
    public string? ExceptionType { get; set; }

    public ExceptionStatusCodeAttribute(int statusCode)
    {
        if (statusCode < 100 || statusCode > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode), "HTTP status code must be between 100 and 599.");

        StatusCode = statusCode;
    }
}
