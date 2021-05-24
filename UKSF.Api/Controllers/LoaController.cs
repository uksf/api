using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Controllers
{
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class LoaController : Controller
    {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly IChainOfCommandService _chainOfCommandService;
        private readonly ICommandRequestContext _commandRequestContext;
        private readonly IDisplayNameService _displayNameService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILoaContext _loaContext;
        private readonly ILoaService _loaService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;

        public LoaController(
            ILoaContext loaContext,
            IAccountContext accountContext,
            ICommandRequestContext commandRequestContext,
            IUnitsContext unitsContext,
            ILoaService loaService,
            IHttpContextService httpContextService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IUnitsService unitsService,
            IChainOfCommandService chainOfCommandService,
            INotificationsService notificationsService,
            ILogger logger
        )
        {
            _loaContext = loaContext;
            _accountContext = accountContext;
            _commandRequestContext = commandRequestContext;
            _unitsContext = unitsContext;
            _loaService = loaService;
            _httpContextService = httpContextService;
            _displayNameService = displayNameService;
            _accountService = accountService;
            _unitsService = unitsService;
            _chainOfCommandService = chainOfCommandService;
            _notificationsService = notificationsService;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public LoaReportDataset Get([FromQuery] string scope = "you")
        {
            List<string> objectIds;
            switch (scope)
            {
                case "all":
                    objectIds = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER).Select(x => x.Id).ToList();
                    break;
                case "unit":
                    DomainAccount domainAccount = _accountService.GetUserAccount();
                    IEnumerable<Unit> groups = _unitsService.GetAllChildren(_unitsContext.GetSingle(x => x.Name == domainAccount.UnitAssignment), true);
                    List<string> members = groups.SelectMany(x => x.Members.ToList()).ToList();
                    objectIds = _accountContext.Get(x => x.MembershipState == MembershipState.MEMBER && members.Contains(x.Id)).Select(x => x.Id).ToList();
                    break;
                case "you":
                    objectIds = new() { _httpContextService.GetUserId() };
                    break;
                default: throw new InvalidLoaScopeException(scope);
            }

            IEnumerable<LoaReport> loaReports = _loaService.Get(objectIds)
                                                           .Select(
                                                               x => new LoaReport
                                                               {
                                                                   Id = x.Id,
                                                                   Start = x.Start,
                                                                   End = x.End,
                                                                   State = x.State,
                                                                   Emergency = x.Emergency,
                                                                   Late = x.Late,
                                                                   Reason = x.Reason,
                                                                   Name = _displayNameService.GetDisplayName(_accountContext.GetSingle(x.Recipient)),
                                                                   InChainOfCommand = _chainOfCommandService.InContextChainOfCommand(x.Recipient),
                                                                   LongTerm = (x.End - x.Start).Days > 21
                                                               }
                                                           )
                                                           .ToList();
            return new()
            {
                ActiveLoas = loaReports.Where(x => x.Start <= DateTime.Now && x.End > DateTime.Now).OrderBy(x => x.End).ThenBy(x => x.Start).ToList(),
                UpcomingLoas = loaReports.Where(x => x.Start >= DateTime.Now).OrderBy(x => x.Start).ThenBy(x => x.End).ToList(),
                PastLoas = loaReports.Where(x => x.End < DateTime.Now).OrderByDescending(x => x.End).ThenByDescending(x => x.Start).ToList()
            };
        }

        [HttpDelete("{id}"), Authorize]
        public async Task DeleteLoa(string id)
        {
            Loa loa = _loaContext.GetSingle(id);
            CommandRequest request = _commandRequestContext.GetSingle(x => x.Value == id);
            if (request != null)
            {
                await _commandRequestContext.Delete(request);
                foreach (string reviewerId in request.Reviews.Keys.Where(x => x != request.Requester))
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

                _logger.LogAudit($"Loa request deleted for '{_displayNameService.GetDisplayName(_accountContext.GetSingle(loa.Recipient))}' from '{loa.Start}' to '{loa.End}'");
            }

            _logger.LogAudit($"Loa deleted for '{_displayNameService.GetDisplayName(_accountContext.GetSingle(loa.Recipient))}' from '{loa.Start}' to '{loa.End}'");
            await _loaContext.Delete(loa);
        }
    }
}
