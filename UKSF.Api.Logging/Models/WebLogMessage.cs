using System;

namespace UKSF.Api.Logging.Models {
    public class WebLogMessage : BasicLogMessage {
        public string exception;
        public string httpMethod;
        public string name;
        public string url;
        public string userId;

        public WebLogMessage() { }

        public WebLogMessage(Exception logException) {
            message = logException.GetBaseException().Message;
            exception = logException.ToString();
            level = LogLevel.ERROR;
        }
    }
}
