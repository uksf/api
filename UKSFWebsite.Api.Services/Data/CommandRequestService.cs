using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Models.CommandRequests;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Hubs.Abstraction;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Data {
    public class CommandRequestService : CachedDataService<CommandRequest>, ICommandRequestService {
        private const string DATABASE_ARCHIVE_COLLECTION = "commandRequestsArchive";
        private readonly IAccountService accountService;
        private readonly IChainOfCommandService chainOfCommandService;
        private readonly IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub;
        private readonly IDisplayNameService displayNameService;
        private readonly INotificationsService notificationsService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;
        private readonly IRanksService ranksService;

        public CommandRequestService(
            IMongoDatabase database,
            INotificationsService notificationsService,
            ISessionService sessionService,
            IDisplayNameService displayNameService,
            IAccountService accountService,
            IChainOfCommandService chainOfCommandService,
            IUnitsService unitsService,
            IRanksService ranksService,
            IHubContext<CommandRequestsHub, ICommandRequestsClient> commandRequestsHub
        ) : base(database, "commandRequests") {
            this.notificationsService = notificationsService;
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
            this.accountService = accountService;
            this.chainOfCommandService = chainOfCommandService;
            this.unitsService = unitsService;
            this.ranksService = ranksService;
            this.commandRequestsHub = commandRequestsHub;
        }

        public async Task Add(CommandRequest request, ChainOfCommandMode mode = ChainOfCommandMode.COMMANDER_AND_ONE_ABOVE) {
            Account requesterAccount = sessionService.GetContextAccount();
            Account recipientAccount = accountService.GetSingle(request.recipient);
            request.displayRequester = displayNameService.GetDisplayName(requesterAccount);
            request.displayRecipient = displayNameService.GetDisplayName(recipientAccount);
            HashSet<string> ids = chainOfCommandService.ResolveChain(mode, unitsService.GetSingle(x => x.name == recipientAccount.unitAssignment), unitsService.GetSingle(request.value)).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
            List<Account> accounts = ids.Select(x => accountService.GetSingle(x)).OrderBy(x => x.rank, new RankComparer(ranksService)).ThenBy(x => x.lastname).ThenBy(x => x.firstname).ToList();
            foreach (Account account in accounts) {
                request.reviews.Add(account.id, ReviewState.PENDING);
            }

            await base.Add(request);
            LogWrapper.AuditLog(sessionService.GetContextId(), $"{request.type} request created for {request.displayRecipient} from {request.displayFrom} to {request.displayValue} because '{request.reason}'");
            foreach (Account account in accounts.Where(x => x.id != requesterAccount.id)) {
                notificationsService.Add(new Notification {owner = account.id, icon = NotificationIcons.REQUEST, message = $"{request.displayRequester} requires your review on {AvsAn.Query(request.type).Article} {request.type.ToLower()} request for {request.displayRecipient}", link = "/command/requests"});
            }

            Refresh();
            await commandRequestsHub.Clients.All.ReceiveRequestUpdate();
        }

        public async Task ArchiveRequest(string id) {
            CommandRequest request = GetSingle(id);
            await Database.GetCollection<CommandRequest>(DATABASE_ARCHIVE_COLLECTION).InsertOneAsync(request);
            await Delete(id);
            Refresh();
        }

        public async Task SetRequestReviewState(CommandRequest request, string reviewerId, ReviewState newState) {
            await Update(request.id, Builders<CommandRequest>.Update.Set($"reviews.{reviewerId}", newState));
            Refresh();
        }

        public async Task SetRequestAllReviewStates(CommandRequest request, ReviewState newState, string overriderId) {
            List<string> keys = new List<string>(request.reviews.Keys);
            foreach (string key in keys) {
                request.reviews[key] = newState;
            }

            foreach (string id in request.reviews.Select(x => x.Key).Where(x => x != overriderId)) {
                notificationsService.Add(new Notification {owner = id, icon = NotificationIcons.REQUEST, message = $"Your review on {AvsAn.Query(request.type).Article} {request.type.ToLower()} request for {request.displayRecipient} was overriden by {overriderId}"});
            }

            await Update(request.id, Builders<CommandRequest>.Update.Set("reviews", request.reviews));
            Refresh();
        }

        public ReviewState GetReviewState(string id, string reviewer) {
            CommandRequest request = GetSingle(id);
            return request == null
                ? ReviewState.ERROR
                : !request.reviews.ContainsKey(reviewer)
                    ? ReviewState.ERROR
                    : request.reviews[reviewer];
        }

        public bool IsRequestApproved(string id) => GetSingle(id).reviews.All(x => x.Value == ReviewState.APPROVED);

        public bool IsRequestRejected(string id) => GetSingle(id).reviews.Any(x => x.Value == ReviewState.REJECTED);

        public bool DoesEquivalentRequestExist(CommandRequest request) {
            return Get().Any(x => x.recipient == request.recipient && x.type == request.type && x.displayValue == request.displayValue && x.displayFrom == request.displayFrom);
        }
    }
}
