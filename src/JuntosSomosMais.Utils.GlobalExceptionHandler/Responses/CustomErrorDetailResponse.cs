namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Responses;

public class CustomErrorDetailResponse
{
    public string? Type { get; set; }
    public int StatusCode { get; set; }
    public CustomErrorDetail? Error { get; set; }

    public CustomErrorDetailResponse() { }
    public CustomErrorDetailResponse(string type, int statusCode, CustomErrorDetail error)
    {
        Type = type;
        StatusCode = statusCode;
        Error = error;
    }
}

public class CustomErrorDetail
{
    public string? RequestId { get; set; }
    public string? Msg { get; set; }
    public string? Detail { get; set; }

    public CustomErrorDetail() { }
    public CustomErrorDetail(string requestId, string msg, string? detail)
    {
        RequestId = requestId;
        Msg = msg;
        Detail = detail;
    }
}
