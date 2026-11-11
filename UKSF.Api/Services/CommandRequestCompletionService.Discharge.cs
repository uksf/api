using MongoDB.Driver;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public partial class CommandRequestCompletionService
{
    private async Task Discharge(DomainCommandRequest request)
    {
        if (commandRequestService.IsRequestApproved(request.Id))
        {
            var account = accountContext.GetSingle(request.Recipient);
            DomainDischarge discharge = new()
            {
                Rank = account.Rank,
                Unit = account.UnitAssignment,
                Role = account.RoleAssignment,
                DischargedBy = request.DisplayRequester,
                Reason = request.Reason
            };
            var dischargeCollection = dischargeContext.GetSingle(x => x.AccountId == account.Id);
            if (dischargeCollection == null)
            {
                dischargeCollection = new DomainDischargeCollection { AccountId = account.Id, Name = $"{account.Lastname}.{account.Firstname[0]}" };
                dischargeCollection.Discharges.Add(discharge);
                await dischargeContext.Add(dischargeCollection);
            }
            else
            {
                dischargeCollection.Discharges.Add(discharge);
                await dischargeContext.Update(
                    dischargeCollection.Id,
                    Builders<DomainDischargeCollection>.Update.Set(x => x.Reinstated, false)
                                                       .Set(x => x.Name, $"{account.Lastname}.{account.Firstname[0]}")
                                                       .Set(x => x.Discharges, dischargeCollection.Discharges)
                );
            }

            await accountContext.Update(account.Id, x => x.MembershipState, MembershipState.Discharged);

            var notification = await assignmentService.UpdateUnitRankAndRole(
                account.Id,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                AssignmentService.RemoveFlag,
                request.Reason,
                "",
                AssignmentService.RemoveFlag
            );
            notificationsService.Add(notification);
            await assignmentService.UnassignAllUnits(account.Id);
        }

        await ResolveAndAudit(request);
    }
}
