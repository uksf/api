using System;
using UKSFWebsite.Api.Models.Logging;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ILogging {
        void Log(string message);
        void Log(BasicLogMessage log);
        void Log(Exception exception);
    }
}
