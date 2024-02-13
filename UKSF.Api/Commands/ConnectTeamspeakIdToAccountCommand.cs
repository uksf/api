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

public class ConnectTeamspeakIdToAccountCommand : IConnectTeamspeakIdToAccountCommand
{
    private readonly IAccountContext _accountContext;
    private readonly IConfirmationCodeService _confirmationCodeService;
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;
    private readonly INotificationsService _notificationsService;

    public ConnectTeamspeakIdToAccountCommand(
        IEventBus eventBus,
        IUksfLogger logger,
        IAccountContext accountContext,
        IConfirmationCodeService confirmationCodeService,
        INotificationsService notificationsService
    )
    {
        _eventBus = eventBus;
        _logger = logger;
        _accountContext = accountContext;
        _confirmationCodeService = confirmationCodeService;
        _notificationsService = notificationsService;
    }

    public async Task<DomainAccount> ExecuteAsync(string accountId, string teamspeakId, string code)
    {
        var domainAccount = _accountContext.GetSingle(accountId);
        if (await _confirmationCodeService.GetConfirmationCodeValue(code) != teamspeakId)
        {
            await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == teamspeakId);
            throw new InvalidConfirmationCodeException();
        }

        domainAccount.TeamspeakIdentities ??= new HashSet<int>();
        domainAccount.TeamspeakIdentities.Add(int.Parse(teamspeakId));
        await _accountContext.Update(domainAccount.Id, Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, domainAccount.TeamspeakIdentities));

        var updatedAccount = _accountContext.GetSingle(domainAccount.Id);
        _eventBus.Send(updatedAccount);
        _notificationsService.SendTeamspeakNotification(
            new HashSet<int> { teamspeakId.ToInt() },
            $"This teamspeak identity has been linked to the account with email '{updatedAccount.Email}'\nIf this was not done by you, please contact an admin"
        );
        _logger.LogAudit($"Teamspeak ID ({teamspeakId}) linked to account {updatedAccount.Id}");

        return updatedAccount;
    }
}
