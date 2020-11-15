using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Shared.Models {
    public enum LogLevel {
        DEBUG,
        INFO,
        ERROR,
        WARNING
    }

    public class BasicLog : DatabaseObject {
        public LogLevel level = LogLevel.INFO;
        public string message;
        public DateTime timestamp = DateTime.UtcNow;

        protected BasicLog() { }

        public BasicLog(string text) : this() => message = text;

        public BasicLog(string text, LogLevel logLevel) : this() {
            message = text;
            level = logLevel;
        }

        public BasicLog(Exception exception) : this() {
            message = exception.GetBaseException().ToString();
            level = LogLevel.ERROR;
        }
    }
}
