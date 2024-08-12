using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

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
                new CommandRequest
                {
                    Recipient = discharge.AccountId,
                    Type = CommandRequestType.ReinstateMember,
                    DisplayValue = "Member",
                    DisplayFrom = "Discharged"
                }
            );
        }

        return discharges;
    }

    [HttpGet("reinstate/{id}")]
    public async Task<IEnumerable<DischargeCollection>> Reinstate([FromRoute] string id)
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
                new Notification
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
