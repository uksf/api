﻿using System;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Shared.Events {
    public interface ILogger {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception);
        void LogHttpError(Exception exception);
        void LogHttpError(HttpErrorLog log);
        void LogAudit(string message, string userId = "");
        void LogDiscordEvent(DiscordUserEventType discordUserEventType, string instigatorId, string instigatorName, string channelName, string name, string message);
    }

    public class Logger : ILogger {
        private readonly IHttpContextService _httpContextService;
        private readonly IEventBus _eventBus;

        public Logger(IHttpContextService httpContextService, IEventBus eventBus) {
            _httpContextService = httpContextService;
            _eventBus = eventBus;
        }

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
            userId = string.IsNullOrEmpty(userId) ? _httpContextService.GetUserId() ?? "Server" : userId;
            Log(new AuditLog(userId, message));
        }

        public void LogDiscordEvent(DiscordUserEventType discordUserEventType, string instigatorId, string instigatorName, string channelName, string name, string message) {
            Log(new DiscordLog(discordUserEventType, instigatorId, instigatorName, channelName, name, message));
        }

        private void Log(BasicLog log) {
            _eventBus.Send(new LoggerEventData(log));
        }
    }
}