using System;

namespace UKSF.Api.Base.Models.Logging {
    public class HttpErrorLog : BasicLog {
        public string exception;
        public string httpMethod;
        public string name;
        public string url;
        public string userId;

        public HttpErrorLog() { }

        public HttpErrorLog(Exception exception) {
            message = exception.GetBaseException().Message;
            this.exception = exception.ToString();
            level = LogLevel.ERROR;
        }
    }
}
