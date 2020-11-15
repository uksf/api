using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class LoggingController : Controller {
        private readonly IAuditLogDataService auditLogDataService;
        private readonly IHttpErrorLogDataService httpErrorLogDataService;
        private readonly ILauncherLogDataService launcherLogDataService;
        private readonly ILogDataService logDataService;

        public LoggingController(
            ILogDataService logDataService,
            IAuditLogDataService auditLogDataService,
            IHttpErrorLogDataService httpErrorLogDataService,
            ILauncherLogDataService launcherLogDataService
        ) {
            this.logDataService = logDataService;
            this.auditLogDataService = auditLogDataService;
            this.httpErrorLogDataService = httpErrorLogDataService;
            this.launcherLogDataService = launcherLogDataService;
        }

        [HttpGet("basic"), Authorize]
        public List<BasicLog> GetBasicLogs() {
            List<BasicLog> logs = new List<BasicLog>(logDataService.Get());
            logs.Reverse();
            return logs;
        }

        [HttpGet("httpError"), Authorize]
        public List<HttpErrorLog> GetHttpErrorLogs() {
            List<HttpErrorLog> errorLogs = new List<HttpErrorLog>(httpErrorLogDataService.Get());
            errorLogs.Reverse();
            return errorLogs;
        }

        [HttpGet("audit"), Authorize]
        public List<AuditLog> GetAuditLogs() {
            List<AuditLog> auditLogs = new List<AuditLog>(auditLogDataService.Get());
            auditLogs.Reverse();
            return auditLogs;
        }

        [HttpGet("launcher"), Authorize]
        public List<LauncherLog> GetLauncherLogs() {
            List<LauncherLog> launcherLogs = new List<LauncherLog>(launcherLogDataService.Get());
            launcherLogs.Reverse();
            return launcherLogs;
        }
    }
}
