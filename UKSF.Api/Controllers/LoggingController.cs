using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.ADMIN)]
    public class LoggingController : Controller {
        private readonly IMongoDatabase database;

        public LoggingController(IMongoDatabase database) => this.database = database;

        [HttpGet, Authorize]
        public IActionResult GetLogs([FromQuery] string type = "logs") {
            switch (type) {
                case "error":
                    List<WebLogMessage> errorLogs = database.GetCollection<WebLogMessage>("errorLogs").AsQueryable().ToList();
                    errorLogs.Reverse();
                    return Ok(errorLogs);
                case "audit":
                    List<AuditLogMessage> auditLogs = database.GetCollection<AuditLogMessage>("auditLogs").AsQueryable().ToList();
                    auditLogs.Reverse();
                    return Ok(auditLogs);
                case "launcher":
                    List<LauncherLogMessage> launcherLogs = database.GetCollection<LauncherLogMessage>("launcherLogs").AsQueryable().ToList();
                    launcherLogs.Reverse();
                    return Ok(launcherLogs);
                default:
                    List<BasicLogMessage> logs = database.GetCollection<BasicLogMessage>("logs").AsQueryable().ToList();
                    logs.Reverse();
                    return Ok(logs);
            }
        }
    }
}
