namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;

public class CustomValidationErrorResponse
{
    public string? Type { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, string[]>? Error { get; set; }

    public CustomValidationErrorResponse() { }
    public CustomValidationErrorResponse(string type, int statusCode, Dictionary<string, string[]> error)
    {
        Type = type;
        StatusCode = statusCode;
        Error = error;
    }
}
