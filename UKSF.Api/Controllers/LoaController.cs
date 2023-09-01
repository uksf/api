using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class LoaController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly ICommandRequestContext _commandRequestContext;
    private readonly IDisplayNameService _displayNameService;
    private readonly IGetPagedLoasQuery _getPagedLoasQuery;
    private readonly ILoaContext _loaContext;
    private readonly ILoaMapper _loaMapper;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;

    public LoaController(
        ILoaContext loaContext,
        IAccountContext accountContext,
        ICommandRequestContext commandRequestContext,
        IDisplayNameService displayNameService,
        INotificationsService notificationsService,
        IUksfLogger logger,
        IGetPagedLoasQuery getPagedLoasQuery,
        ILoaMapper loaMapper
    )
    {
        _loaContext = loaContext;
        _accountContext = accountContext;
        _commandRequestContext = commandRequestContext;
        _displayNameService = displayNameService;
        _notificationsService = notificationsService;
        _logger = logger;
        _getPagedLoasQuery = getPagedLoasQuery;
        _loaMapper = loaMapper;
    }

    [HttpGet]
    public async Task<PagedResult<Loa>> GetPaged(
        [FromQuery] int page,
        [FromQuery] int pageSize = 15,
        [FromQuery] string query = null,
        [FromQuery] LoaSelectionMode selectionMode = default,
        [FromQuery] LoaDateMode dateMode = default,
        [FromQuery] DateTime? selectedDate = null,
        [FromQuery] LoaViewMode viewMode = default
    )
    {
        var pagedResult = await _getPagedLoasQuery.ExecuteAsync(new(page, pageSize, query, selectionMode, dateMode, selectedDate, viewMode));

        return new(pagedResult.TotalCount, pagedResult.Data.Select(_loaMapper.MapToLoa).ToList());
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteLoa([FromRoute] string id)
    {
        var domainLoa = _loaContext.GetSingle(id);
        var request = _commandRequestContext.GetSingle(x => x.Value == id);
        if (request != null)
        {
            await _commandRequestContext.Delete(request);
            foreach (var reviewerId in request.Reviews.Keys.Where(x => x != request.Requester))
            {
                _notificationsService.Add(
                    new()
                    {
                        Owner = reviewerId,
                        Icon = NotificationIcons.Request,
                        Message = $"Your review for {request.DisplayRequester}'s LOA is no longer required as they deleted their LOA",
                        Link = "/command/requests"
                    }
                );
            }

            _logger.LogAudit(
                $"Loa request deleted for {_displayNameService.GetDisplayName(_accountContext.GetSingle(domainLoa.Recipient))} from {domainLoa.Start:dd MMM yyyy} to {domainLoa.End:dd MMM yyyy}"
            );
        }

        _logger.LogAudit(
            $"Loa deleted for {_displayNameService.GetDisplayName(_accountContext.GetSingle(domainLoa.Recipient))} from {domainLoa.Start:dd MMM yyyy} to {domainLoa.End:dd MMM yyyy}"
        );
        await _loaContext.Delete(domainLoa);
    }
}
