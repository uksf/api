using System;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models.Message.Logging;

namespace UKSF.Api.Interfaces.Message {
    public interface ILoggingService : IDataBackedService<ILogDataService> {
        void Log(string message);
        void Log(BasicLogMessage log);
        void Log(Exception exception);
        void AuditLog(string message, string userId = "");
    }
}
