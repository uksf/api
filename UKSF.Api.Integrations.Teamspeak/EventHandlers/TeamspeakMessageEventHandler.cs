using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers {
    public interface ITeamspeakMessageEventHandler : IEventHandler { }

    public class TeamspeakMessageEventHandler : ITeamspeakMessageEventHandler {
        private readonly IEventBus<TeamspeakMessageEventModel> _accountEventBus;
        private readonly ILogger _logger;
        private readonly ITeamspeakService _teamspeakService;

        public TeamspeakMessageEventHandler(IEventBus<TeamspeakMessageEventModel> accountEventBus, ILogger logger, ITeamspeakService teamspeakService) {
            _accountEventBus = accountEventBus;
            _logger = logger;
            _teamspeakService = teamspeakService;
        }

        public void Init() {
            _accountEventBus.AsObservable().SubscribeWithAsyncNext(HandleMessageEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleMessageEvent(TeamspeakMessageEventModel messageEvent) {
            await _teamspeakService.SendTeamspeakMessageToClient(messageEvent.ClientDbIds, messageEvent.Message);
        }
    }
}
