using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Models.CommandRequests;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Controllers.CommandRequests {
    [Route("[controller]"), Roles(RoleDefinitions.COMMAND)]
    public class CommandRequestsController : Controller {
        private readonly ICommandRequestCompletionService commandRequestCompletionService;
        private readonly ICommandRequestService commandRequestService;
        private readonly IDisplayNameService displayNameService;
        private readonly ISessionService sessionService;
        private readonly IUnitsService unitsService;
        private readonly INotificationsService notificationsService;

        public CommandRequestsController(
            ICommandRequestService commandRequestService,
            ICommandRequestCompletionService commandRequestCompletionService,
            ISessionService sessionService,
            IUnitsService unitsService,
            IDisplayNameService displayNameService,
            INotificationsService notificationsService
        ) {
            this.commandRequestService = commandRequestService;
            this.commandRequestCompletionService = commandRequestCompletionService;
            this.sessionService = sessionService;
            this.unitsService = unitsService;
            this.displayNameService = displayNameService;
            this.notificationsService = notificationsService;
        }

        [HttpGet, Authorize]
        public IActionResult Get() {
            List<CommandRequest> allRequests = commandRequestService.Get();
            List<CommandRequest> myRequests = new List<CommandRequest>();
            List<CommandRequest> otherRequests = new List<CommandRequest>();
            string contextId = sessionService.GetContextId();
            bool canOverride = unitsService.GetSingle(x => x.shortname == "SR10").members.Any(x => x == contextId);
            bool superAdmin = contextId == Global.SUPER_ADMIN;
            DateTime now = DateTime.Now;
            foreach (CommandRequest commandRequest in allRequests) {
                Dictionary<string, ReviewState>.KeyCollection reviewers = commandRequest.reviews.Keys;
                if (reviewers.Any(k => k == contextId)) {
                    myRequests.Add(commandRequest);
                } else {
                    otherRequests.Add(commandRequest);
                }
            }

            return Ok(
                new {
                    myRequests = myRequests.Select(
                        x => {
                            if (string.IsNullOrEmpty(x.reason)) x.reason = "None given";
                            x.type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.type.ToLower());
                            return new {
                                data = x,
                                canOverride = superAdmin || canOverride && x.reviews.Count > 1 && x.dateCreated.AddDays(1) < now && x.reviews.Any(y => y.Value == ReviewState.PENDING && y.Key != contextId),
                                reviews = x.reviews.Select(y => new {id = y.Key, name = displayNameService.GetDisplayName(y.Key), state = y.Value})
                            };
                        }
                    ),
                    otherRequests = otherRequests.Select(
                        x => {
                            if (string.IsNullOrEmpty(x.reason)) x.reason = "None given";
                            x.type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.type.ToLower());
                            return new {
                                data = x,
                                canOverride = superAdmin || canOverride && x.dateCreated.AddDays(1) < now,
                                reviews = x.reviews.Select(y => new {name = displayNameService.GetDisplayName(y.Key), state = y.Value})
                            };
                        }
                    )
                }
            );
        }

        [HttpPatch("{id}"), Authorize]
        public async Task<IActionResult> UpdateRequestReview(string id, [FromBody] JObject body) {
            bool overriden = bool.Parse(body["overriden"].ToString());
            ReviewState state = Enum.Parse<ReviewState>(body["reviewState"].ToString());
            Account sessionAccount = sessionService.GetContextAccount();
            CommandRequest request = commandRequestService.GetSingle(id);
            if (request == null) {
                throw new NullReferenceException($"Failed to get request with id {id}, does not exist");
            }
            if (overriden) {
                LogWrapper.AuditLog(sessionAccount.id, $"Review state of {request.type.ToLower()} request for {request.displayRecipient} overriden to {state}");
                await commandRequestService.SetRequestAllReviewStates(request, state);

                foreach (string reviewerId in request.reviews.Select(x => x.Key).Where(x => x != sessionAccount.id)) {
                    notificationsService.Add(new Notification {owner = reviewerId, icon = NotificationIcons.REQUEST, message = $"Your review on {AvsAn.Query(request.type).Article} {request.type.ToLower()} request for {request.displayRecipient} was overriden by {sessionAccount.id}"});
                }
            } else {
                ReviewState currentState = commandRequestService.GetReviewState(request.id, sessionAccount.id);
                if (currentState == ReviewState.ERROR) {
                    throw new ArgumentOutOfRangeException($"Getting review state for {sessionAccount} from {request.id} failed. Reviews: \n{request.reviews.Select(x => $"{x.Key}: {x.Value}").Aggregate((x, y) => $"{x}\n{y}")}");
                }
                if (currentState == state) return Ok();
                LogWrapper.AuditLog(sessionAccount.id, $"Review state of {displayNameService.GetDisplayName(sessionAccount)} for {request.type.ToLower()} request for {request.displayRecipient} updated to {state}");
                await commandRequestService.SetRequestReviewState(request, sessionAccount.id, state);
            }

            try {
                await commandRequestCompletionService.Resolve(request.id);
            } catch (Exception) {
                if (overriden) {
                    await commandRequestService.SetRequestAllReviewStates(request, ReviewState.PENDING);
                } else {
                    await commandRequestService.SetRequestReviewState(request, sessionAccount.id, ReviewState.PENDING);
                }
                throw;
            }

            return Ok();
        }

        [HttpPost("exists"), Authorize]
        public IActionResult RequestExists([FromBody] CommandRequest request) => Ok(commandRequestService.DoesEquivalentRequestExist(request));
    }
}
