using System;

namespace UKSF.Api.Shared.Models {
    public class HttpErrorLog : BasicLog {
        public HttpErrorLog(Exception exception) {
            Exception = exception.ToString();
            Message = exception.GetBaseException().Message;
            Level = LogLevel.ERROR;
        }

        public HttpErrorLog(Exception exception, string name, string userId, string httpMethod, string url) : this(exception) {
            Name = name;
            UserId = userId;
            HttpMethod = httpMethod;
            Url = url;
        }

        public string Exception;
        public string HttpMethod;
        public string Name;
        public string Url;
        public string UserId;
    }
}
