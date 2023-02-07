using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Core.Models;

public class ErrorLog : BasicLog
{
    public ErrorLog(Exception exception, string url, string method, string endpointName, int statusCode, string userId, string name)
    {
        Level = UksfLogLevel.ERROR;
        Exception = exception.GetCompleteString();
        Message = exception.GetBaseException().Message;
        Url = url;
        Method = method;
        EndpointName = endpointName;
        StatusCode = statusCode;
        UserId = userId;
        Name = name;
    }

    public ErrorLog(Exception exception)
    {
        Level = UksfLogLevel.ERROR;
        Exception = exception.GetCompleteString();
        Message = exception.GetBaseException().Message;
    }

    public string EndpointName { get; set; }
    public string Exception { get; set; }
    public string Method { get; set; }
    public string Name { get; set; }
    public int StatusCode { get; set; }
    public string Url { get; set; }
    public string UserId { get; set; }
}
