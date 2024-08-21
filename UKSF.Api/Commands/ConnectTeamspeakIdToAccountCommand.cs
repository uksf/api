using MongoDB.Driver;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;

namespace UKSF.Api.Commands;

public interface IConnectTeamspeakIdToAccountCommand
{
    Task<DomainAccount> ExecuteAsync(string accountId, string teamspeakId, string code);
}

public class ConnectTeamspeakIdToAccountCommand(
    IUksfLogger logger,
    IAccountContext accountContext,
    IConfirmationCodeService confirmationCodeService,
    INotificationsService notificationsService
) : IConnectTeamspeakIdToAccountCommand
{
    public async Task<DomainAccount> ExecuteAsync(string accountId, string teamspeakId, string code)
    {
        var account = accountContext.GetSingle(accountId);
        if (await confirmationCodeService.GetConfirmationCodeValue(code) != teamspeakId)
        {
            await confirmationCodeService.ClearConfirmationCodes(x => x.Value == teamspeakId);
            throw new InvalidConfirmationCodeException();
        }

        account.TeamspeakIdentities ??= new HashSet<int>();
        account.TeamspeakIdentities.Add(int.Parse(teamspeakId));
        await accountContext.Update(account.Id, Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, account.TeamspeakIdentities));

        var updatedAccount = accountContext.GetSingle(account.Id);
        notificationsService.SendTeamspeakNotification(
            new HashSet<int> { teamspeakId.ToInt() },
            $"This teamspeak identity has been linked to the account with email '{updatedAccount.Email}'\nIf this was not done by you, please contact an admin"
        );
        logger.LogAudit($"Teamspeak ID ({teamspeakId}) linked to account {updatedAccount.Id}");

        return updatedAccount;
    }
}
