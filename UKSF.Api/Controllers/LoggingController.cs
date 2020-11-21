using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class LoggingController : Controller {
        private readonly IAuditLogContext _auditLogContext;
        private readonly IDiscordLogContext _discordLogContext;
        private readonly IHttpErrorLogContext _httpErrorLogContext;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly ILogContext _logContext;

        public LoggingController(
            ILogContext logContext,
            IAuditLogContext auditLogContext,
            IHttpErrorLogContext httpErrorLogContext,
            ILauncherLogContext launcherLogContext,
            IDiscordLogContext discordLogContext
        ) {
            _logContext = logContext;
            _auditLogContext = auditLogContext;
            _httpErrorLogContext = httpErrorLogContext;
            _launcherLogContext = launcherLogContext;
            _discordLogContext = discordLogContext;
        }

        // TODO: Pagination

        [HttpGet("basic"), Authorize]
        public List<BasicLog> GetBasicLogs() {
            List<BasicLog> logs = new(_logContext.Get());
            logs.Reverse();
            return logs;
        }

        [HttpGet("httpError"), Authorize]
        public List<HttpErrorLog> GetHttpErrorLogs() {
            List<HttpErrorLog> logs = new(_httpErrorLogContext.Get());
            logs.Reverse();
            return logs;
        }

        [HttpGet("audit"), Authorize]
        public List<AuditLog> GetAuditLogs() {
            List<AuditLog> logs = new(_auditLogContext.Get());
            logs.Reverse();
            return logs;
        }

        [HttpGet("launcher"), Authorize]
        public List<LauncherLog> GetLauncherLogs() {
            List<LauncherLog> logs = new(_launcherLogContext.Get());
            logs.Reverse();
            return logs;
        }

        [HttpGet("discord"), Authorize]
        public List<DiscordLog> GetDiscordLogs() {
            List<DiscordLog> logs = new(_discordLogContext.Get());
            logs.Reverse();
            return logs;
        }
    }
}
