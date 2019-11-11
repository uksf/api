using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Command;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Personnel;

namespace UKSFWebsite.Api.Services.Command {
    public class CommandRequestService : ICommandRequestService {
        private readonly IAccountService accountService;
        private readonly IChainOfCommandService chainOfCommandService;
        private readonly ICommandRequestDataService data;
        private readonly ICommandRequestArchiveDataService dataArchive;
        private readonly IDisplayNameService displayNameService;
        private readonly INotificationsService notificationsService;
        private readonly IRanksService ranksService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;

        public CommandRequestService(
            ICommandRequestDataService data,
            ICommandRequestArchiveDataService dataArchive,
            INotificationsService notificationsService,
            ISessionService sessionService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IChainOfCommandService chainOfCommandService,
            IUnitsService unitsService,
            IRanksService ranksService
        ) {
            this.data = data;
            this.dataArchive = dataArchive;
            this.notificationsService = notificationsService;
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
            this.accountService = accountService;
            this.chainOfCommandService = chainOfCommandService;
            this.unitsService = unitsService;
            this.ranksService = ranksService;
        }

        public ICommandRequestDataService Data() => data;

        public async Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE) {
            Account requesterAccount = sessionService.GetContextAccount();
            Account recipientAccount = accountService.Data().GetSingle(request.recipient);
            request.displayRequester = displayNameService.GetDisplayName(requesterAccount);
            request.displayRecipient = displayNameService.GetDisplayName(recipientAccount);
            HashSet<string> ids = chainOfCommandService.ResolveChain(mode, recipientAccount.id, unitsService.Data().GetSingle(x => x.name == recipientAccount.unitAssignment), unitsService.Data().GetSingle(request.value));
            if (ids.Count == 0) throw new Exception($"Failed to get any commanders for review for {request.type.ToLower()} request for {request.displayRecipient}.\nContact an admin");

            List<Account> accounts = ids.Select(x => accountService.Data().GetSingle(x)).OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();
            foreach (Account account in accounts) {
                request.reviews.Add(account.id, ReviewState.PENDING);
            }

            await data.Add(request);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request created for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            bool selfRequest = request.displayRequester == request.displayRecipient;
            string notificationMessage = $"{request.displayRequester} requires your review on {(selfRequest ? "their" : AvsAn.Query(request.type).Article)} {request.type.ToLower()} request{(selfRequest ? "" : $" for {request.displayRecipient}")}";
            foreach (Account account in accounts.Where(x => x.id != requesterAccount.id)) {
                notificationsService.Add(new Notification {owner = account.id, icon = NotificationIcons.REQUEST, message = notificationMessage, link = "/command/requests"});
            }
        }

        public async Task ArchiveRequest(string id) {
            CommandRequest request = data.GetSingle(id);
            await dataArchive.Add(request);
            await data.Delete(id);
        }

        public async Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState) {
            await data.Update(request.id, Builders<CommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
        }

        public async Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState) {
            List<string> keys = new List<string>(request.reviews.Keys);
            foreach (string key in keys) {
                request.reviews[key] = newState;
            }

            await data.Update(request.id, Builders<CommandRequest>.Update.Set("reviews", request.reviews));
        }

        public ReviewState GetReviewState(string id, string reviewer) {
            CommandRequest request = data.GetSingle(id);
            return request == null ? ReviewState.ERROR : !request.reviews.ContainsKey(reviewer) ? ReviewState.ERROR : request.reviews[reviewer];
        }

        public bool IsRequestApproved(string id) => data.GetSingle(id).reviews.All(x => x.Value == ReviewState.APPROVED);

        public bool IsRequestRejected(string id) => data.GetSingle(id).reviews.Any(x => x.Value == ReviewState.REJECTED);

        public bool DoesEquivalentRequestExist(CommandRequest request) {
            return data.Get().Any(x => x.recipient == request.recipient && x.type == request.type && x.displayValue == request.displayValue && x.displayFrom == request.displayFrom);
        }
    }
}
