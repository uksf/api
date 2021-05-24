using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Exceptions;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.Commands
{
    public interface IConnectTeamspeakIdToAccountCommand
    {
        Task<DomainAccount> ExecuteAsync(ConnectTeamspeakIdToAccountCommandArgs args);
    }

    public class ConnectTeamspeakIdToAccountCommandArgs
    {
        public ConnectTeamspeakIdToAccountCommandArgs(string accountId, string teamspeakId, string code)
        {
            AccountId = accountId;
            TeamspeakId = teamspeakId;
            Code = code;
        }

        public string AccountId { get; }
        public string TeamspeakId { get; }
        public string Code { get; }
    }

    public class ConnectTeamspeakIdToAccountCommand : IConnectTeamspeakIdToAccountCommand
    {
        private readonly IAccountContext _accountContext;
        private readonly IConfirmationCodeService _confirmationCodeService;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;

        public ConnectTeamspeakIdToAccountCommand(
            IEventBus eventBus,
            ILogger logger,
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

        public async Task<DomainAccount> ExecuteAsync(ConnectTeamspeakIdToAccountCommandArgs args)
        {
            DomainAccount domainAccount = _accountContext.GetSingle(args.AccountId);
            string teamspeakId = await _confirmationCodeService.GetConfirmationCodeValue(args.Code);
            if (string.IsNullOrWhiteSpace(teamspeakId) || teamspeakId != args.TeamspeakId)
            {
                await _confirmationCodeService.ClearConfirmationCodes(x => x.Value == args.TeamspeakId);
                throw new InvalidConfirmationCodeException();
            }

            domainAccount.TeamspeakIdentities ??= new();
            domainAccount.TeamspeakIdentities.Add(int.Parse(teamspeakId));
            await _accountContext.Update(domainAccount.Id, Builders<DomainAccount>.Update.Set(x => x.TeamspeakIdentities, domainAccount.TeamspeakIdentities));

            DomainAccount updatedAccount = _accountContext.GetSingle(domainAccount.Id);
            _eventBus.Send(updatedAccount);
            _notificationsService.SendTeamspeakNotification(
                new HashSet<int> { teamspeakId.ToInt() },
                $"This teamspeak identity has been linked to the account with email '{updatedAccount.Email}'\nIf this was not done by you, please contact an admin"
            );
            _logger.LogAudit($"Teamspeak ID {teamspeakId} added for {updatedAccount.Id}");

            return updatedAccount;
        }
    }
}
