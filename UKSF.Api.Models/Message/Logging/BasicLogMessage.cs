using System;

namespace UKSF.Api.Models.Message.Logging {
    public enum LogLevel {
        DEBUG,
        INFO,
        ERROR
    }

    public class BasicLogMessage : MongoObject {
        public LogLevel level = LogLevel.INFO;
        public string message;
        public DateTime timestamp = DateTime.UtcNow;

        protected BasicLogMessage() { }

        public BasicLogMessage(string text) : this() => message = text;

        public BasicLogMessage(LogLevel logLevel) : this() => level = logLevel;

        public BasicLogMessage(string text, LogLevel logLevel) : this() {
            message = text;
            level = logLevel;
        }

        public BasicLogMessage(Exception logException) : this() {
            message = logException.GetBaseException().ToString();
            level = LogLevel.ERROR;
        }
    }
}
