using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IMedicAttachmentService
{
    Task<bool> SeverAttachment(string accountId, string trigger);
    HashSet<string> ResolveAttachmentReviewers(DomainAccount recipient, string newTroopId, string oldTroopId);
}

public class MedicAttachmentService(
    IAccountContext accountContext,
    IUnitsContext unitsContext,
    IChainOfCommandService chainOfCommandService,
    IDisplayNameService displayNameService,
    INotificationsService notificationsService,
    IUksfLogger logger
) : IMedicAttachmentService
{
    public async Task<bool> SeverAttachment(string accountId, string trigger)
    {
        var account = accountContext.GetSingle(accountId);
        if (account is null || string.IsNullOrEmpty(account.AttachedTroop))
        {
            return false;
        }

        var troop = unitsContext.GetSingle(account.AttachedTroop);
        var troopName = troop?.Name ?? "their troop";
        await accountContext.Update(accountId, x => x.AttachedTroop, null);
        logger.LogAudit($"Medic attachment for {displayNameService.GetDisplayName(account)} to {troopName} removed ({trigger})");

        Notify(account.Id, $"Your medic attachment to {troopName} was removed ({trigger})");
        NotifyCommanders(account, troop, $"{displayNameService.GetDisplayName(account)}'s medic attachment to {troopName} was removed ({trigger})");
        return true;
    }

    public HashSet<string> ResolveAttachmentReviewers(DomainAccount recipient, string newTroopId, string oldTroopId)
    {
        var reviewers = new HashSet<string>();
        var sfmUnit = unitsContext.GetSingle(x => x.Name == recipient.UnitAssignment);
        reviewers.UnionWith(chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, recipient.Id, sfmUnit, null));
        if (!string.IsNullOrEmpty(newTroopId))
        {
            reviewers.UnionWith(chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, recipient.Id, unitsContext.GetSingle(newTroopId), null));
        }

        if (!string.IsNullOrEmpty(oldTroopId))
        {
            reviewers.UnionWith(chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, recipient.Id, unitsContext.GetSingle(oldTroopId), null));
        }

        reviewers.Remove(recipient.Id);
        return reviewers;
    }

    private void NotifyCommanders(DomainAccount account, DomainUnit troop, string message)
    {
        var sfmUnit = unitsContext.GetSingle(x => x.Name == account.UnitAssignment);
        foreach (var commanderId in new[] { sfmUnit?.ChainOfCommand?.First, troop?.ChainOfCommand?.First }
                     .Where(x => !string.IsNullOrEmpty(x) && x != account.Id).Distinct())
        {
            Notify(commanderId, message);
        }
    }

    private void Notify(string owner, string message)
    {
        notificationsService.Add(new DomainNotification { Owner = owner, Icon = Icons.MedicAttachment, Message = message, Link = "/units" });
    }
}
