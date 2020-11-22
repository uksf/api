using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AvsAnLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Command.Controllers {
    [Route("[controller]"), Permissions(Permissions.COMMAND)]
    public class CommandRequestsController : Controller {
        private const string SUPER_ADMIN = "59e38f10594c603b78aa9dbd";
        private readonly IAccountService _accountService;
        private readonly ICommandRequestCompletionService _commandRequestCompletionService;
        private readonly ICommandRequestContext _commandRequestContext;
        private readonly ICommandRequestService _commandRequestService;
        private readonly IDisplayNameService _displayNameService;
        private readonly IHttpContextService _httpContextService;
        private readonly ILogger _logger;
        private readonly INotificationsService _notificationsService;
        private readonly IUnitsContext _unitsContext;
        private readonly IVariablesContext _variablesContext;

        public CommandRequestsController(
            ICommandRequestService commandRequestService,
            ICommandRequestCompletionService commandRequestCompletionService,
            IHttpContextService httpContextService,
            IUnitsContext unitsContext,
            ICommandRequestContext commandRequestContext,
            IDisplayNameService displayNameService,
            INotificationsService notificationsService,
            IVariablesContext variablesContext,
            IAccountService accountService,
            ILogger logger
        ) {
            _commandRequestService = commandRequestService;
            _commandRequestCompletionService = commandRequestCompletionService;
            _httpContextService = httpContextService;
            _unitsContext = unitsContext;
            _commandRequestContext = commandRequestContext;
            _displayNameService = displayNameService;
            _notificationsService = notificationsService;
            _variablesContext = variablesContext;
            _accountService = accountService;
            _logger = logger;
        }

        [HttpGet, Authorize]
        public IActionResult Get() {
            IEnumerable<CommandRequest> allRequests = _commandRequestContext.Get();
            List<CommandRequest> myRequests = new();
            List<CommandRequest> otherRequests = new();
            string contextId = _httpContextService.GetUserId();
            string id = _variablesContext.GetSingle("UNIT_ID_PERSONNEL").AsString();
            bool canOverride = _unitsContext.GetSingle(id).Members.Any(x => x == contextId);
            bool superAdmin = contextId == SUPER_ADMIN;
            DateTime now = DateTime.Now;
            foreach (CommandRequest commandRequest in allRequests) {
                Dictionary<string, ReviewState>.KeyCollection reviewers = commandRequest.Reviews.Keys;
                if (reviewers.Any(k => k == contextId)) {
                    myRequests.Add(commandRequest);
                } else {
                    otherRequests.Add(commandRequest);
                }
            }

            return Ok(new { myRequests = GetMyRequests(myRequests, contextId, canOverride, superAdmin, now), otherRequests = GetOtherRequests(otherRequests, canOverride, superAdmin, now) });
        }

        private object GetMyRequests(IEnumerable<CommandRequest> myRequests, string contextId, bool canOverride, bool superAdmin, DateTime now) {
            return myRequests.Select(
                x => {
                    if (string.IsNullOrEmpty(x.Reason)) x.Reason = "None given";
                    x.Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Type.ToLower());
                    return new {
                        data = x,
                        canOverride = superAdmin || canOverride && x.Reviews.Count > 1 && x.DateCreated.AddDays(1) < now && x.Reviews.Any(y => y.Value == ReviewState.PENDING && y.Key != contextId),
                        reviews = x.Reviews.Select(y => new { id = y.Key, name = _displayNameService.GetDisplayName(y.Key), state = y.Value })
                    };
                }
            );
        }

        private object GetOtherRequests(IEnumerable<CommandRequest> otherRequests, bool canOverride, bool superAdmin, DateTime now) {
            return otherRequests.Select(
                x => {
                    if (string.IsNullOrEmpty(x.Reason)) x.Reason = "None given";
                    x.Type = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Type.ToLower());
                    return new {
                        data = x,
                        canOverride = superAdmin || canOverride && x.DateCreated.AddDays(1) < now,
                        reviews = x.Reviews.Select(y => new { name = _displayNameService.GetDisplayName(y.Key), state = y.Value })
                    };
                }
            );
        }

        [HttpPatch("{id}"), Authorize]
        public async Task<IActionResult> UpdateRequestReview(string id, [FromBody] JObject body) {
            bool overriden = bool.Parse(body["overriden"].ToString());
            ReviewState state = Enum.Parse<ReviewState>(body["reviewState"].ToString());
            Account sessionAccount = _accountService.GetUserAccount();
            CommandRequest request = _commandRequestContext.GetSingle(id);
            if (request == null) {
                throw new NullReferenceException($"Failed to get request with id {id}, does not exist");
            }

            if (overriden) {
                _logger.LogAudit($"Review state of {request.Type.ToLower()} request for {request.DisplayRecipient} overriden to {state}");
                await _commandRequestService.SetRequestAllReviewStates(request, state);

                foreach (string reviewerId in request.Reviews.Select(x => x.Key).Where(x => x != sessionAccount.Id)) {
                    _notificationsService.Add(
                        new Notification {
                            Owner = reviewerId,
                            Icon = NotificationIcons.REQUEST,
                            Message = $"Your review on {AvsAn.Query(request.Type).Article} {request.Type.ToLower()} request for {request.DisplayRecipient} was overriden by {sessionAccount.Id}"
                        }
                    );
                }
            } else {
                ReviewState currentState = _commandRequestService.GetReviewState(request.Id, sessionAccount.Id);
                if (currentState == ReviewState.ERROR) {
                    throw new ArgumentOutOfRangeException(
                        $"Getting review state for {sessionAccount} from {request.Id} failed. Reviews: \n{request.Reviews.Select(x => $"{x.Key}: {x.Value}").Aggregate((x, y) => $"{x}\n{y}")}"
                    );
                }

                if (currentState == state) return Ok();
                _logger.LogAudit($"Review state of {_displayNameService.GetDisplayName(sessionAccount)} for {request.Type.ToLower()} request for {request.DisplayRecipient} updated to {state}");
                await _commandRequestService.SetRequestReviewState(request, sessionAccount.Id, state);
            }

            try {
                await _commandRequestCompletionService.Resolve(request.Id);
            } catch (Exception) {
                if (overriden) {
                    await _commandRequestService.SetRequestAllReviewStates(request, ReviewState.PENDING);
                } else {
                    await _commandRequestService.SetRequestReviewState(request, sessionAccount.Id, ReviewState.PENDING);
                }

                throw;
            }

            return Ok();
        }

        [HttpPost("exists"), Authorize]
        public IActionResult RequestExists([FromBody] CommandRequest request) => Ok(_commandRequestService.DoesEquivalentRequestExist(request));
    }
}
