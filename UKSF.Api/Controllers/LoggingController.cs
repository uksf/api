using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using SortDirection = UKSF.Api.Core.Models.SortDirection;

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
        [FromQuery] string filter,
        [FromQuery] string levels
    )
    {
        var filterProperties = GetBasicLogFilterProperties();
        var additionalFilter = ParseEnumFilter<DomainBasicLog, UksfLogLevel>(levels, x => x.Level);
        return logContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter, additionalFilter);
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
        [FromQuery] string filter,
        [FromQuery] string eventTypes
    )
    {
        var filterProperties = GetDiscordLogFilterProperties();
        var additionalFilter = ParseEnumFilter<DiscordLog, DiscordUserEventType>(eventTypes, x => x.DiscordUserEventType);
        return discordLogContext.GetPaged(page, pageSize, sortDirection, sortField, filterProperties, filter, additionalFilter);
    }

    private static IEnumerable<Expression<Func<DomainBasicLog, object>>> GetBasicLogFilterProperties()
    {
        return new List<Expression<Func<DomainBasicLog, object>>> { x => x.Message, x => x.Level };
    }

    private static IEnumerable<Expression<Func<ErrorLog, object>>> GetErrorLogFilterProperties()
    {
        return new List<Expression<Func<ErrorLog, object>>>
        {
            x => x.Message,
            x => x.StatusCode,
            x => x.Url,
            x => x.Name,
            x => x.Exception,
            x => x.UserId,
            x => x.Method,
            x => x.EndpointName
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
            x => x.Message,
            x => x.DiscordUserEventType,
            x => x.InstigatorId,
            x => x.InstigatorName,
            x => x.ChannelName,
            x => x.Name
        };
    }

    private static FilterDefinition<TLog> ParseEnumFilter<TLog, TEnum>(string commaSeparatedValues, Expression<Func<TLog, TEnum>> fieldSelector)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(commaSeparatedValues))
        {
            return null;
        }

        var values = commaSeparatedValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                         .Select(v => Enum.TryParse<TEnum>(v, ignoreCase: true, out var parsed) ? parsed : (TEnum?)null)
                                         .Where(v => v.HasValue)
                                         .Select(v => v.Value)
                                         .ToList();

        return values.Count == 0 ? null : Builders<TLog>.Filter.In(fieldSelector, values);
    }
}
