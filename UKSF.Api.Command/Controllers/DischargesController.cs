using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Personnel, Permissions.Nco, Permissions.Recruiter)]
public class DischargesController : ControllerBase
{
    private readonly IAccountContext _accountContext;
    private readonly IAssignmentService _assignmentService;
    private readonly ICommandRequestService _commandRequestService;
    private readonly IDischargeContext _dischargeContext;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;
    private readonly IUnitsContext _unitsContext;
    private readonly IVariablesContext _variablesContext;

    public DischargesController(
        IDischargeContext dischargeContext,
        IAccountContext accountContext,
        IUnitsContext unitsContext,
        IAssignmentService assignmentService,
        ICommandRequestService commandRequestService,
        INotificationsService notificationsService,
        IHttpContextService httpContextService,
        IVariablesContext variablesContext,
        IUksfLogger logger
    )
    {
        _dischargeContext = dischargeContext;
        _accountContext = accountContext;
        _unitsContext = unitsContext;
        _assignmentService = assignmentService;
        _commandRequestService = commandRequestService;
        _notificationsService = notificationsService;
        _httpContextService = httpContextService;
        _variablesContext = variablesContext;
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<DischargeCollection> Get()
    {
        IEnumerable<DischargeCollection> discharges = _dischargeContext.Get().ToList();
        foreach (var discharge in discharges)
        {
            discharge.RequestExists = _commandRequestService.DoesEquivalentRequestExist(
                new() { Recipient = discharge.AccountId, Type = CommandRequestType.ReinstateMember, DisplayValue = "Member", DisplayFrom = "Discharged" }
            );
        }

        return discharges;
    }

    [HttpGet("reinstate/{id}")]
    public async Task<IEnumerable<DischargeCollection>> Reinstate(string id)
    {
        var dischargeCollection = _dischargeContext.GetSingle(id);
        await _dischargeContext.Update(dischargeCollection.Id, Builders<DischargeCollection>.Update.Set(x => x.Reinstated, true));
        await _accountContext.Update(dischargeCollection.AccountId, x => x.MembershipState, MembershipState.MEMBER);
        var notification = await _assignmentService.UpdateUnitRankAndRole(
            dischargeCollection.AccountId,
            "Basic Training Unit",
            "Trainee",
            "Recruit",
            "",
            "",
            "your membership was reinstated"
        );
        _notificationsService.Add(notification);

        _logger.LogAudit($"{_httpContextService.GetUserId()} reinstated {dischargeCollection.Name}'s membership", _httpContextService.GetUserId());
        var personnelId = _variablesContext.GetSingle("UNIT_ID_PERSONNEL").AsString();
        foreach (var member in _unitsContext.GetSingle(personnelId).Members.Where(x => x != _httpContextService.GetUserId()))
        {
            _notificationsService.Add(
                new()
                {
                    Owner = member,
                    Icon = NotificationIcons.Promotion,
                    Message = $"{dischargeCollection.Name}'s membership was reinstated by {_httpContextService.GetUserId()}"
                }
            );
        }

        return _dischargeContext.Get();
    }
}
