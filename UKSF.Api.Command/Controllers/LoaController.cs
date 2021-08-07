using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Mappers;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Queries;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Controllers
{
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class LoaController : ControllerBase
    {
        private readonly IAccountContext _accountContext;
        private readonly ICommandRequestContext _commandRequestContext;
        private readonly IDisplayNameService _displayNameService;
        private readonly IGetPagedLoasQuery _getPagedLoasQuery;
        private readonly ILoaContext _loaContext;
        private readonly ILoaMapper _loaMapper;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;

        public LoaController(
            ILoaContext loaContext,
            IAccountContext accountContext,
            ICommandRequestContext commandRequestContext,
            IDisplayNameService displayNameService,
            INotificationsService notificationsService,
            ILogger logger,
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
            [FromQuery] LoaViewMode viewMode = default
        )
        {
            var pagedResult = await _getPagedLoasQuery.ExecuteAsync(new(page, pageSize, query, selectionMode, viewMode));

            return new(pagedResult.TotalCount, pagedResult.Data.Select(_loaMapper.MapToLoa).ToList());
        }

        [HttpDelete("{id}"), Authorize]
        public async Task DeleteLoa(string id)
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
                            Icon = NotificationIcons.REQUEST,
                            Message = $"Your review for {request.DisplayRequester}'s LOA is no longer required as they deleted their LOA",
                            Link = "/command/requests"
                        }
                    );
                }

                _logger.LogAudit(
                    $"Loa request deleted for '{_displayNameService.GetDisplayName(_accountContext.GetSingle(domainLoa.Recipient))}' from '{domainLoa.Start}' to '{domainLoa.End}'"
                );
            }

            _logger.LogAudit(
                $"Loa deleted for '{_displayNameService.GetDisplayName(_accountContext.GetSingle(domainLoa.Recipient))}' from '{domainLoa.Start}' to '{domainLoa.End}'"
            );
            await _loaContext.Delete(domainLoa);
        }
    }
}
