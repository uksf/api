using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;

namespace UKSF.Api.Commands;

public interface IConnectTeamspeakIdToAccountCommand
{
    Task<DomainAccount> ExecuteAsync(string accountId, string teamspeakId, string code);
}

public class ConnectTeamspeakIdToAccountCommand(
    IEventBus eventBus,
    IUksfLogger logger,
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    INotificationsService notificationsService
) : IConnectTeamspeakIdToAccountCommand
{
    public async Task<DomainAccount> ExecuteAsync(string accountId, string teamspeakId, string code)
    {
        var domainAccount = accountContext.GetSingle(accountId);
        if (await confirmationCodeService.GetConfirmationCodeValue(code) != teamspeakId)
        {
            await confirmationCodeService.ClearConfirmationCodes(x => x.Value == teamspeakId);
            throw new InvalidConfirmationCodeException();
        }

        domainAccount.TeamspeakIdentities ??= new HashSet<int>();
        domainAccount.TeamspeakIdentities.Add(int.Parse(teamspeakId));
        await accountContext.Update(domainAccount.Id, Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, domainAccount.TeamspeakIdentities));

        var updatedAccount = accountContext.GetSingle(domainAccount.Id);
        eventBus.Send(new ContextEventData<DomainAccount>(accountId, updatedAccount));
        notificationsService.SendTeamspeakNotification(
            new HashSet<int> { teamspeakId.ToInt() },
            $"This teamspeak identity has been linked to the account with email '{updatedAccount.Email}'\nIf this was not done by you, please contact an admin"
        );
        logger.LogAudit($"Teamspeak ID ({teamspeakId}) linked to account {updatedAccount.Id}");

        return updatedAccount;
    }
}
