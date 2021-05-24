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
            DomainAccount requesterDomainAccount = _accountService.GetUserAccount();
            DomainAccount recipientDomainAccount = _accountContext.GetSingle(request.Recipient);
            request.DisplayRequester = _displayNameService.GetDisplayName(requesterDomainAccount);
            request.DisplayRecipient = _displayNameService.GetDisplayName(recipientDomainAccount);
            HashSet<string> ids = _chainOfCommandService.ResolveChain(
                mode,
                recipientDomainAccount.Id,
                _unitsContext.GetSingle(x => x.Name == recipientDomainAccount.UnitAssignment),
                _unitsContext.GetSingle(request.Value)
            );
            if (ids.Count == 0)
            {
                throw new($"Failed to get any commanders for review for {request.Type.ToLower()} request for {request.DisplayRecipient}.\nContact an admin");
            }

            List<DomainAccount> accounts = ids.Select(x => _accountContext.GetSingle(x))
                                              .OrderBy(x => x.Rank, new RankComparer(_ranksService))
                                              .ThenBy(x => x.Lastname)
                                              .ThenBy(x => x.Firstname)
                                              .ToList();
            foreach (DomainAccount account in accounts)
            {
                request.Reviews.Add(account.Id, ReviewState.PENDING);
            }

            await _commandRequestContext.Add(request);
            _logger.LogAudit($"{request.Type} request created for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            bool selfRequest = request.DisplayRequester == request.DisplayRecipient;
            string notificationMessage =
                $"{request.DisplayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.Type).Article)} {request.Type.ToLower()} request{(selfRequest ? "" : $" for {request.DisplayRecipient}")}";
            foreach (DomainAccount account in accounts.Where(x => x.Id != requesterDomainAccount.Id))
            {
                _notificationsService.Add(new() { Owner = account.Id, Icon = NotificationIcons.REQUEST, Message = notificationMessage, Link = "/command/requests" });
            }
        }

        public async Task ArchiveRequest(string id)
        {
            CommandRequest request = _commandRequestContext.GetSingle(id);
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
            foreach (string key in keys)
            {
                request.Reviews[key] = newState;
            }

            await _commandRequestContext.Update(request.Id, Builders<CommandRequest>.Update.Set("reviews", request.Reviews));
        }

        public ReviewState GetReviewState(string id, string reviewer)
        {
            CommandRequest request = _commandRequestContext.GetSingle(id);
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
    }
}
