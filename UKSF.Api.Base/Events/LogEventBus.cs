﻿using System;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Base.Events {
    public interface ILogger : IEventBus<BasicLog> {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception);
        void LogHttpError(Exception exception);
        void LogHttpError(HttpErrorLog log);
        void LogAudit(string message, string userId = "");
    }

    public class Logger : EventBus<BasicLog>, ILogger {
        private readonly IHttpContextService httpContextService;

        public Logger(IHttpContextService httpContextService) => this.httpContextService = httpContextService;

        public void LogInfo(string message) {
            Log(new BasicLog(message, LogLevel.INFO));
        }

        public void LogWarning(string message) {
            Log(new BasicLog(message, LogLevel.WARNING));
        }

        public void LogError(string message) {
            Log(new BasicLog(message, LogLevel.ERROR));
        }

        public void LogError(Exception exception) {
            Log(new BasicLog(exception));
        }

        public void LogHttpError(Exception exception) {
            Log(new HttpErrorLog(exception));
        }

        public void LogHttpError(HttpErrorLog log) {
            Log(log);
        }

        public void LogAudit(string message, string userId = "") {
            userId = string.IsNullOrEmpty(userId) ? httpContextService.GetUserId() ?? "Server" : userId;
            Log(new AuditLog(userId, message));
        }

        private void Log(BasicLog log) {
            Send(log);
        }
    }
}