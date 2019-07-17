using System;
using Microsoft.Extensions.DependencyInjection;
using UKSFWebsite.Api.Models.Logging;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Utility {
    public static class LogWrapper {
        public static void Log(string message) => ServiceWrapper.ServiceProvider.GetService<ILogging>().Log(message);

        public static void Log(BasicLogMessage log) => ServiceWrapper.ServiceProvider.GetService<ILogging>().Log(log);

        public static void Log(Exception exception) => ServiceWrapper.ServiceProvider.GetService<ILogging>().Log(exception);

        public static void AuditLog(string userId, string message) => Log(new AuditLogMessage {who = userId, level = LogLevel.INFO, message = message});
    }
}
