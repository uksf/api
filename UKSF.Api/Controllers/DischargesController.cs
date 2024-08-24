using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Personnel, Permissions.Nco, Permissions.Recruiter)]
public class DischargesController(
    IDischargeContext dischargeContext,
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IAssignmentService assignmentService,
    ICommandRequestService commandRequestService,
    INotificationsService notificationsService,
    IHttpContextService httpContextService,
    IVariablesContext variablesContext,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    public IEnumerable<DomainDischargeCollection> Get()
    {
        IEnumerable<DomainDischargeCollection> discharges = dischargeContext.Get().ToList();
        foreach (var discharge in discharges)
        {
            discharge.RequestExists = commandRequestService.DoesEquivalentRequestExist(
                new DomainCommandRequest
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
    public async Task<IEnumerable<DomainDischargeCollection>> Reinstate([FromRoute] string id)
    {
        var dischargeCollection = dischargeContext.GetSingle(id);
        await dischargeContext.Update(dischargeCollection.Id, Builders<DomainDischargeCollection>.Update.Set(x => x.Reinstated, true));
        await accountContext.Update(dischargeCollection.AccountId, x => x.MembershipState, MembershipState.Member);
        var notification = await assignmentService.UpdateUnitRankAndRole(
            dischargeCollection.AccountId,
            "Basic Training Unit",
            "Trainee",
            "Recruit",
            "",
            "",
            "your membership was reinstated"
        );
        notificationsService.Add(notification);

        logger.LogAudit($"{httpContextService.GetUserId()} reinstated {dischargeCollection.Name}'s membership", httpContextService.GetUserId());
        var personnelId = variablesContext.GetSingle("UNIT_ID_PERSONNEL").AsString();
        foreach (var member in unitsContext.GetSingle(personnelId).Members.Where(x => x != httpContextService.GetUserId()))
        {
            notificationsService.Add(
                new DomainNotification
                {
                    Owner = member,
                    Icon = NotificationIcons.Promotion,
                    Message = $"{dischargeCollection.Name}'s membership was reinstated by {httpContextService.GetUserId()}"
                }
            );
        }

        return dischargeContext.Get();
    }
}
