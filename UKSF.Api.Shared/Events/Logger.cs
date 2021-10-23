using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Shared.Events
{
    public interface ILogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception);
        void LogError(Exception exception, HttpContext context, HttpResponse response, string userId, string userDisplayName);
        void LogAudit(string message, string userId = null);
        void LogDiscordEvent(DiscordUserEventType discordUserEventType, string instigatorId, string instigatorName, string channelName, string name, string message);
    }

    public class Logger : ILogger
    {
        private readonly IEventBus _eventBus;
        private readonly IHttpContextService _httpContextService;

        public Logger(IHttpContextService httpContextService, IEventBus eventBus)
        {
            _httpContextService = httpContextService;
            _eventBus = eventBus;
        }

        public void LogInfo(string message)
        {
            Log(new(message, LogLevel.INFO));
        }

        public void LogWarning(string message)
        {
            Log(new(message, LogLevel.WARNING));
        }

        public void LogError(string message)
        {
            Log(new(message, LogLevel.ERROR));
        }

        public void LogError(Exception exception)
        {
            Log(new(exception));
        }

        public void LogAudit(string message, string userId = null)
        {
            userId = string.IsNullOrEmpty(userId) ? _httpContextService.GetUserId() ?? "Server" : userId;
            Log(new AuditLog(userId, message));
        }

        public void LogDiscordEvent(DiscordUserEventType discordUserEventType, string instigatorId, string instigatorName, string channelName, string name, string message)
        {
            Log(new DiscordLog(discordUserEventType, instigatorId, instigatorName, channelName, name, message));
        }

        public void LogError(Exception exception, HttpContext context, HttpResponse response, string userId, string userDisplayName)
        {
            var controllerActionDescriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var endpointName = controllerActionDescriptor == null
                ? null
                : controllerActionDescriptor.ControllerName + "." + controllerActionDescriptor.ActionName;
            Log(new ErrorLog(exception, context.Request.Path + context.Request.QueryString, context.Request.Method, endpointName, response?.StatusCode ?? 500, userId, userDisplayName));
        }

        private void Log(BasicLog log)
        {
            _eventBus.Send(new LoggerEventData(log));
        }
    }
}
