using System;

namespace UKSF.Api.Shared.Models
{
    public class ErrorLog : BasicLog
    {
        public string EndpointName;
        public string Exception;
        public string Method;
        public string Name;
        public int StatusCode;
        public string Url;
        public string UserId;

        public ErrorLog(Exception exception, string url, string method, string endpointName, int statusCode, string userId, string name)
        {
            Level = LogLevel.ERROR;
            Exception = exception.ToString();
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
            Level = LogLevel.ERROR;
            Exception = exception.ToString();
            Message = exception.GetBaseException().Message;
        }
    }
}
