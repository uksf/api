using System.Threading.Tasks;
using MongoDB.Bson;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Personnel.EventHandlers {
    public interface IDiscordEventhandler : IEventHandler { }

    public class DiscordEventhandler : IDiscordEventhandler {
        private readonly IAccountContext _accountContext;
        private readonly ICommentThreadService _commentThreadService;
        private readonly IDisplayNameService _displayNameService;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        public DiscordEventhandler(IEventBus eventBus, ICommentThreadService commentThreadService, IAccountContext accountContext, IDisplayNameService displayNameService, ILogger logger) {
            _eventBus = eventBus;
            _commentThreadService = commentThreadService;
            _accountContext = accountContext;
            _displayNameService = displayNameService;
            _logger = logger;
        }

        public void Init() {
            _eventBus.AsObservable().SubscribeWithAsyncNext<DiscordEventData>(HandleEvent, _logger.LogError);
        }

        private async Task HandleEvent(EventModel eventModel, DiscordEventData discordEventData) {
            switch (discordEventData.EventType) {
                case DiscordUserEventType.JOINED: break;
                case DiscordUserEventType.LEFT:
                    await LeftEvent(discordEventData.EventData);
                    break;
                case DiscordUserEventType.BANNED:          break;
                case DiscordUserEventType.UNBANNED:        break;
                case DiscordUserEventType.MESSAGE_DELETED: break;
            }
        }

        private async Task LeftEvent(string accountId) {
            Account account = _accountContext.GetSingle(accountId);
            // if (account.MembershipState == MembershipState.CONFIRMED) {
                string name = _displayNameService.GetDisplayName(account);
                await _commentThreadService.InsertComment(account.Application.RecruiterCommentThread, new Comment { Author = ObjectId.Empty.ToString(), Content = $"{name} left the Discord" });
            // }
        }
    }
}
