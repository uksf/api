using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers {
    public interface ITeamspeakAccountEventHandler : IEventHandler { }

    // TODO: Come up with better naming and a better structure for event handlers in components (multiple components can consume the same event, can't all be called XHandler)
    public class TeamspeakAccountEventHandler : ITeamspeakAccountEventHandler {
        private readonly IEventBus<Account> _accountEventBus;
        private readonly ILogger _logger;
        private readonly ITeamspeakService _teamspeakService;

        public TeamspeakAccountEventHandler(IEventBus<Account> accountEventBus, ILogger logger, ITeamspeakService teamspeakService) {
            _accountEventBus = accountEventBus;
            _logger = logger;
            _teamspeakService = teamspeakService;
        }

        public void Init() {
            _accountEventBus.AsObservable().SubscribeWithAsyncNext(HandleAccountEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleAccountEvent(Account account) {
            await _teamspeakService.UpdateAccountTeamspeakGroups(account);
        }
    }
}
