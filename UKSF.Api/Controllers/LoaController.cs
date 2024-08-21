using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class LoaController(
    ILoaContext loaContext,
    IAccountContext accountContext,
    ICommandRequestContext commandRequestContext,
    IDisplayNameService displayNameService,
    INotificationsService notificationsService,
    IUksfLogger logger,
    IGetPagedLoasQuery getPagedLoasQuery,
    ILoaMapper loaMapper
) : ControllerBase
{
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
        var pagedResult =
            await getPagedLoasQuery.ExecuteAsync(new GetPagedLoasQueryArgs(page, pageSize, query, selectionMode, dateMode, selectedDate, viewMode));

        return new PagedResult<Loa>(pagedResult.TotalCount, pagedResult.Data.Select(loaMapper.MapToLoa).ToList());
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteLoa([FromRoute] string id)
    {
        var loa = loaContext.GetSingle(id);
        var request = commandRequestContext.GetSingle(x => x.Value == id);
        if (request != null)
        {
            await commandRequestContext.Delete(request);
            foreach (var reviewerId in request.Reviews.Keys.Where(x => x != request.Requester))
            {
                notificationsService.Add(
                    new DomainNotification
                    {
                        Owner = reviewerId,
                        Icon = NotificationIcons.Request,
                        Message = $"Your review for {request.DisplayRequester}'s LOA is no longer required as they deleted their LOA",
                        Link = "/command/requests"
                    }
                );
            }

            logger.LogAudit(
                $"Loa request deleted for {displayNameService.GetDisplayName(accountContext.GetSingle(loa.Recipient))} from {loa.Start:dd MMM yyyy} to {loa.End:dd MMM yyyy}"
            );
        }

        logger.LogAudit(
            $"Loa deleted for {displayNameService.GetDisplayName(accountContext.GetSingle(loa.Recipient))} from {loa.Start:dd MMM yyyy} to {loa.End:dd MMM yyyy}"
        );
        await loaContext.Delete(loa);
    }
}
