using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using MongoDB.Driver;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Command.Services
{
    public interface ICommandRequestService
    {
        Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE);
        Task ArchiveRequest(string id);
        Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState);
        Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState);
        ReviewState GetReviewState(string id, string reviewer);
        bool IsRequestApproved(string id);
        bool IsRequestRejected(string id);
        bool DoesEquivalentRequestExist(CommandRequest request);
    }

    public class CommandRequestService : ICommandRequestService
    {
        private readonly IAccountContext _accountContext;
        private readonly IAccountService _accountService;
        private readonly IChainOfCommandService _chainOfCommandService;
        private readonly ICommandRequestContext _commandRequestContext;
        private readonly ICommandRequestArchiveContext _dataArchive;
        private readonly IDisplayNameService _displayNameService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IRanksService _ranksService;
        private readonly IUnitsContext _unitsContext;

        public CommandRequestService(
            IAccountContext accountContext,
            IUnitsContext unitsContext,
            ICommandRequestContext commandRequestContext,
            ICommandRequestArchiveContext dataArchive,
            INotificationsService notificationsService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IChainOfCommandService chainOfCommandService,
            IRanksService ranksService,
            ILogger logger
        )
        {
            _accountContext = accountContext;
            _unitsContext = unitsContext;
            _commandRequestContext = commandRequestContext;
            _dataArchive = dataArchive;
            _notificationsService = notificationsService;
            _displayNameService = displayNameService;
            _accountService = accountService;
            _chainOfCommandService = chainOfCommandService;
            _ranksService = ranksService;
            _logger = logger;
        }

        public async Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE)
        {
            var requesterDomainAccount = _accountService.GetUserAccount();
            var recipientDomainAccount = _accountContext.GetSingle(request.Recipient);
            request.DisplayRequester = _displayNameService.GetDisplayName(requesterDomainAccount);
            request.DisplayRecipient = _displayNameService.GetDisplayName(recipientDomainAccount);
            var ids = _chainOfCommandService.ResolveChain(
                mode,
                recipientDomainAccount.Id,
                _unitsContext.GetSingle(x => x.Name == recipientDomainAccount.UnitAssignment),
                _unitsContext.GetSingle(request.Value)
            );
            if (ids.Count == 0)
            {
                throw new($"Failed to get any commanders for review for {request.Type.ToLower()} request for {request.DisplayRecipient}.\nContact an admin");
            }

            var accounts = ids.Select(x => _accountContext.GetSingle(x))
                              .OrderBy(x => x.Rank, new RankComparer(_ranksService))
                              .ThenBy(x => x.Lastname)
                              .ThenBy(x => x.Firstname)
                              .ToList();
            foreach (var account in accounts)
            {
                request.Reviews.Add(account.Id, ReviewState.PENDING);
            }

            await _commandRequestContext.Add(request);
            _logger.LogAudit(
                $"{request.Type} request created for {request.DisplayRecipient} from {FormatIfDate(request.DisplayFrom)} to {FormatIfDate(request.DisplayValue)} because '{request.Reason}'"
            );

            var selfRequest = request.DisplayRequester == request.DisplayRecipient;
            var notificationMessage =
                $"{request.DisplayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.Type).Article)} {request.Type.ToLower()} request{(selfRequest ? "" : $" for {request.DisplayRecipient}")}";
            foreach (var account in accounts.Where(x => x.Id != requesterDomainAccount.Id))
            {
                _notificationsService.Add(new() { Owner = account.Id, Icon = NotificationIcons.REQUEST, Message = notificationMessage, Link = "/command/requests" });
            }
        }

        public async Task ArchiveRequest(string id)
        {
            var request = _commandRequestContext.GetSingle(id);
            await _dataArchive.Add(request);
            await _commandRequestContext.Delete(id);
        }

        public async Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState)
        {
            await _commandRequestContext.Update(request.Id, Builders<CommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
        }

        public async Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState)
        {
            List<string> keys = new(request.Reviews.Keys);
            foreach (var key in keys)
            {
                request.Reviews[key] = newState;
            }

            await _commandRequestContext.Update(request.Id, Builders<CommandRequest>.Update.Set("reviews", request.Reviews));
        }

        public ReviewState GetReviewState(string id, string reviewer)
        {
            var request = _commandRequestContext.GetSingle(id);
            return request == null                     ? ReviewState.ERROR :
                !request.Reviews.ContainsKey(reviewer) ? ReviewState.ERROR : request.Reviews[reviewer];
        }

        public bool IsRequestApproved(string id)
        {
            return _commandRequestContext.GetSingle(id).Reviews.All(x => x.Value == ReviewState.APPROVED);
        }

        public bool IsRequestRejected(string id)
        {
            return _commandRequestContext.GetSingle(id).Reviews.Any(x => x.Value == ReviewState.REJECTED);
        }

        public bool DoesEquivalentRequestExist(CommandRequest request)
        {
            return _commandRequestContext.Get().Any(x => x.Recipient == request.Recipient && x.Type == request.Type && x.DisplayValue == request.DisplayValue && x.DisplayFrom == request.DisplayFrom);
        }

        private static string FormatIfDate(string input)
        {
            return DateTime.TryParse(input, out var date) ? $"{date:dd MMM yyyy}" : input;
        }
    }
}
