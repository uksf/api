using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Controllers
{
    [Route("[controller]"), Permissions(Permissions.ADMIN)]
    public class LoggingController : Controller
    {
        private readonly IAuditLogContext _auditLogContext;
        private readonly IDiscordLogContext _discordLogContext;
        private readonly IErrorLogContext _errorLogContext;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly ILogContext _logContext;

        public LoggingController(
            ILogContext logContext,
            IAuditLogContext auditLogContext,
            IErrorLogContext errorLogContext,
            ILauncherLogContext launcherLogContext,
            IDiscordLogContext discordLogContext
        )
        {
            _logContext = logContext;
            _auditLogContext = auditLogContext;
            _errorLogContext = errorLogContext;
            _launcherLogContext = launcherLogContext;
            _discordLogContext = discordLogContext;
        }

        [HttpGet("basic"), Authorize]
        public PagedResult<BasicLog> GetBasicLogs([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] SortDirection sortDirection, [FromQuery] string sortField, [FromQuery] string filter)
        {
            IEnumerable<Expression<Func<BasicLog, object>>> filterProperties = GetBasicLogFilterProperties();
            return _logContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
        }

        [HttpGet("error"), Authorize]
        public PagedResult<ErrorLog> GetErrorLogs([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] SortDirection sortDirection, [FromQuery] string sortField, [FromQuery] string filter)
        {
            IEnumerable<Expression<Func<ErrorLog, object>>> filterProperties = GetErrorLogFilterProperties();
            return _errorLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
        }

        [HttpGet("audit"), Authorize]
        public PagedResult<AuditLog> GetAuditLogs([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] SortDirection sortDirection, [FromQuery] string sortField, [FromQuery] string filter)
        {
            IEnumerable<Expression<Func<AuditLog, object>>> filterProperties = GetAuditLogFilterProperties();
            return _auditLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
        }

        [HttpGet("launcher"), Authorize]
        public PagedResult<LauncherLog> GetLauncherLogs(
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] SortDirection sortDirection,
            [FromQuery] string sortField,
            [FromQuery] string filter
        )
        {
            IEnumerable<Expression<Func<LauncherLog, object>>> filterProperties = GetLauncherLogFilterProperties();
            return _launcherLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
        }

        [HttpGet("discord"), Authorize]
        public PagedResult<DiscordLog> GetDiscordLogs([FromQuery] int page, [FromQuery] int pageSize, [FromQuery] SortDirection sortDirection, [FromQuery] string sortField, [FromQuery] string filter)
        {
            IEnumerable<Expression<Func<DiscordLog, object>>> filterProperties = GetDiscordLogFilterProperties();
            return _discordLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
        }

        private static IEnumerable<Expression<Func<BasicLog, object>>> GetBasicLogFilterProperties()
        {
            return new List<Expression<Func<BasicLog, object>>> { x => x.Message, x => x.Level };
        }

        private static IEnumerable<Expression<Func<ErrorLog, object>>> GetErrorLogFilterProperties()
        {
            return new List<Expression<Func<ErrorLog, object>>> { x => x.Message, x => x.StatusCode, x => x.Url, x => x.Name, x => x.Exception, x => x.UserId, x => x.Method, x => x.EndpointName };
        }

        private static IEnumerable<Expression<Func<AuditLog, object>>> GetAuditLogFilterProperties()
        {
            return new List<Expression<Func<AuditLog, object>>> { x => x.Message, x => x.Who };
        }

        private static IEnumerable<Expression<Func<LauncherLog, object>>> GetLauncherLogFilterProperties()
        {
            return new List<Expression<Func<LauncherLog, object>>> { x => x.Message };
        }

        private static IEnumerable<Expression<Func<DiscordLog, object>>> GetDiscordLogFilterProperties()
        {
            return new List<Expression<Func<DiscordLog, object>>> { x => x.Message, x => x.DiscordUserEventType, x => x.InstigatorId, x => x.InstigatorName, x => x.ChannelName, x => x.Name };
        }
    }
}
