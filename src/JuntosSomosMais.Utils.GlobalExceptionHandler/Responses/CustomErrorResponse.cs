namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;

public class CustomErrorResponse
{
    public string? Type { get; set; }
    public int StatusCode { get; set; }
    public CustomError? Error { get; set; }

    public CustomErrorResponse() { }
    public CustomErrorResponse(string type, int statusCode, CustomError error)
    {
        Type = type;
        StatusCode = statusCode;
        Error = error;
    }
}

public class CustomError
{
    public string? RequestId { get; set; }
    public string? Msg { get; set; }

    public CustomError() { }
    public CustomError(string requestId, string msg)
    {
        RequestId = requestId;
        Msg = msg;
    }
}
