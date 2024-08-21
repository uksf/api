using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Admin)]
public class LoggingController(
    ILogContext logContext,
    IAuditLogContext auditLogContext,
    IErrorLogContext errorLogContext,
    ILauncherLogContext launcherLogContext,
    IDiscordLogContext discordLogContext
) : ControllerBase
{
    [HttpGet("basic")]
    public PagedResult<DomainBasicLog> GetBasicLogs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] string sortField,
        [FromQuery] string filter
    )
    {
        var filterProperties = GetBasicLogFilterProperties();
        return logContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
    }

    [HttpGet("error")]
    public PagedResult<ErrorLog> GetErrorLogs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] string sortField,
        [FromQuery] string filter
    )
    {
        var filterProperties = GetErrorLogFilterProperties();
        return errorLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
    }

    [HttpGet("audit")]
    public PagedResult<AuditLog> GetAuditLogs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] string sortField,
        [FromQuery] string filter
    )
    {
        var filterProperties = GetAuditLogFilterProperties();
        return auditLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
    }

    [HttpGet("launcher")]
    public PagedResult<LauncherLog> GetLauncherLogs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] string sortField,
        [FromQuery] string filter
    )
    {
        var filterProperties = GetLauncherLogFilterProperties();
        return launcherLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
    }

    [HttpGet("discord")]
    [Authorize]
    public PagedResult<DiscordLog> GetDiscordLogs(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] string sortField,
        [FromQuery] string filter
    )
    {
        var filterProperties = GetDiscordLogFilterProperties();
        return discordLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter);
    }

    private static IEnumerable<Expression<Func<DomainBasicLog, object>>> GetBasicLogFilterProperties()
    {
        return new List<Expression<Func<DomainBasicLog, object>>> { x => x.Message, x => x.Level };
    }

    private static IEnumerable<Expression<Func<ErrorLog, object>>> GetErrorLogFilterProperties()
    {
        return new List<Expression<Func<ErrorLog, object>>>
        {
            x => x.Message, x => x.StatusCode, x => x.Url, x => x.Name, x => x.Exception, x => x.UserId, x => x.Method, x => x.EndpointName
        };
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
        return new List<Expression<Func<DiscordLog, object>>>
        {
            x => x.Message, x => x.DiscordUserEventType, x => x.InstigatorId, x => x.InstigatorName, x => x.ChannelName, x => x.Name
        };
    }
}
