using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.Command.Services {
    public interface ICommandRequestService : IDataBackedService<ICommandRequestDataService> {
        Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE);
        Task ArchiveRequest(string id);
        Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState);
        Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState);
        ReviewState GetReviewState(string id, string reviewer);
        bool IsRequestApproved(string id);
        bool IsRequestRejected(string id);
        bool DoesEquivalentRequestExist(CommandRequest request);
    }

    public class CommandRequestService : DataBackedService<ICommandRequestDataService>, ICommandRequestService {
        private readonly IAccountService _accountService;
        private readonly IChainOfCommandService _chainOfCommandService;
        private readonly ICommandRequestDataService _data;
        private readonly ICommandRequestArchiveDataService _dataArchive;
        private readonly IDisplayNameService _displayNameService;
        private readonly INotificationsService _notificationsService;
        private readonly IRanksService _ranksService;
        private readonly ILogger _logger;

        private readonly IUnitsService _unitsService;

        public CommandRequestService(
            ICommandRequestDataService data,
            ICommandRequestArchiveDataService dataArchive,
            INotificationsService notificationsService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IChainOfCommandService chainOfCommandService,
            IUnitsService unitsService,
            IRanksService ranksService,
            ILogger logger
        ) : base(data) {
            _data = data;
            _dataArchive = dataArchive;
            _notificationsService = notificationsService;

            _displayNameService = displayNameService;
            _accountService = accountService;
            _chainOfCommandService = chainOfCommandService;
            _unitsService = unitsService;
            _ranksService = ranksService;
            _logger = logger;
        }

        public async Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE) {
            Account requesterAccount = _accountService.GetUserAccount();
            Account recipientAccount = _accountService.Data.GetSingle(request.Recipient);
            request.DisplayRequester = _displayNameService.GetDisplayName(requesterAccount);
            request.DisplayRecipient = _displayNameService.GetDisplayName(recipientAccount);
            HashSet<string> ids = _chainOfCommandService.ResolveChain(mode, recipientAccount.id, _unitsService.Data.GetSingle(x => x.name == recipientAccount.unitAssignment), _unitsService.Data.GetSingle(request.Value));
            if (ids.Count == 0) throw new Exception($"Failed to get any commanders for review for {request.Type.ToLower()} request for {request.DisplayRecipient}.\nContact an admin");

            List<Account> accounts = ids.Select(x => _accountService.Data.GetSingle(x)).OrderBy(x => x.rank, new RankComparer(_ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();
            foreach (Account account in accounts) {
                request.Reviews.Add(account.id, ReviewState.PENDING);
            }

            await _data.Add(request);
            _logger.LogAudit($"{request.Type} request created for {request.DisplayRecipient} from {request.DisplayFrom} to {request.DisplayValue} because '{request.Reason}'");
            bool selfRequest = request.DisplayRequester == request.DisplayRecipient;
            string notificationMessage = $"{request.DisplayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.Type).Article)} {request.Type.ToLower()} request{(selfRequest ? "" : $" for {request.DisplayRecipient}")}";
            foreach (Account account in accounts.Where(x => x.id != requesterAccount.id)) {
                _notificationsService.Add(new Notification {owner = account.id, icon = NotificationIcons.REQUEST, message = notificationMessage, link = "/command/requests"});
            }
        }

        public async Task ArchiveRequest(string id) {
            CommandRequest request = _data.GetSingle(id);
            await _dataArchive.Add(request);
            await _data.Delete(id);
        }

        public async Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState) {
            await _data.Update(request.id, Builders<CommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
        }

        public async Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState) {
            List<string> keys = new List<string>(request.Reviews.Keys);
            foreach (string key in keys) {
                request.Reviews[key] = newState;
            }

            await _data.Update(request.id, Builders<CommandRequest>.Update.Set("reviews", request.Reviews));
        }

        public ReviewState GetReviewState(string id, string reviewer) {
            CommandRequest request = _data.GetSingle(id);
            return request == null ? ReviewState.ERROR : !request.Reviews.ContainsKey(reviewer) ? ReviewState.ERROR : request.Reviews[reviewer];
        }

        public bool IsRequestApproved(string id) => _data.GetSingle(id).Reviews.All(x => x.Value == ReviewState.APPROVED);

        public bool IsRequestRejected(string id) => _data.GetSingle(id).Reviews.Any(x => x.Value == ReviewState.REJECTED);

        public bool DoesEquivalentRequestExist(CommandRequest request) {
            return _data.Get().Any(x => x.Recipient == request.Recipient && x.Type == request.Type && x.DisplayValue == request.DisplayValue && x.DisplayFrom == request.DisplayFrom);
        }
    }
}
