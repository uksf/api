﻿using System;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Message {
    public static class LogWrapper {
        public static void Log(string message) => ServiceWrapper.ServiceProvider.GetService<ILoggingService>().Log(message);

        public static void Log(BasicLogMessage log) => ServiceWrapper.ServiceProvider.GetService<ILoggingService>().Log(log);

        public static void Log(Exception exception) => ServiceWrapper.ServiceProvider.GetService<ILoggingService>().Log(exception);

        public static void AuditLog(string userId, string message) => Log(new AuditLogMessage {who = userId, level = LogLevel.INFO, message = message});
    }
}