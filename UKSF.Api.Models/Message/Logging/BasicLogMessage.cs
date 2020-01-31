using System;
using MongoDB.Bson;

namespace UKSF.Api.Models.Message.Logging {
    public enum LogLevel {
        DEBUG,
        INFO,
        ERROR
    }

    public class BasicLogMessage : MongoObject {
        public LogLevel level = LogLevel.INFO;
        public string message;
        public DateTime timestamp;

        protected BasicLogMessage() : this(DateTime.UtcNow) { }

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

        private BasicLogMessage(DateTime time) {
            timestamp = time;
            id = ObjectId.GenerateNewId(time).ToString();
        }
    }
}
