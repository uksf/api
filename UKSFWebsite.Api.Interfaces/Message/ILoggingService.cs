using System;
using UKSFWebsite.Api.Models.Message.Logging;

namespace UKSFWebsite.Api.Interfaces.Message {
    public interface ILoggingService {
        void Log(string message);
        void Log(BasicLogMessage log);
        void Log(Exception exception);
    }
}
