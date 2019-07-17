using System;

namespace UKSFWebsite.Api.Models.Logging {
    public class WebLogMessage : BasicLogMessage {
        public string exception;
        public string httpMethod;
        public string url;
        public string userId;
        public string name;

        public WebLogMessage() { }

        public WebLogMessage(Exception logException) {
            message = logException.GetBaseException().Message;
            exception = logException.ToString();
            level = LogLevel.ERROR;
        }
    }
}
