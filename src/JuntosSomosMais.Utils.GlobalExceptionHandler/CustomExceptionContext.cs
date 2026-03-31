using System.Net;
using Microsoft.AspNetCore.Http;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler;

public class CustomExceptionContext
{
    public required HttpContext HttpContext { get; init; }
    public required Exception Exception { get; init; }
    public required HttpStatusCode StatusCode { get; init; }
    public required string ExceptionType { get; init; }
}
