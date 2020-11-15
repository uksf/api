using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Discord.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Discord.EventHandlers {
    public interface IDiscordAccountEventHandler : IEventHandler { }

    public class DiscordAccountEventHandler : IDiscordAccountEventHandler {
        private readonly IEventBus<Account> _accountEventBus;
        private readonly IDiscordService _discordService;
        private readonly ILogger _logger;

        public DiscordAccountEventHandler(IEventBus<Account> accountEventBus, ILogger logger, IDiscordService discordService) {
            _accountEventBus = accountEventBus;
            _logger = logger;
            _discordService = discordService;
        }

        public void Init() {
            _accountEventBus.AsObservable().SubscribeWithAsyncNext(HandleAccountEvent, exception => _logger.LogError(exception));
        }

        private async Task HandleAccountEvent(Account account) {
            await _discordService.UpdateAccount(account);
        }
    }
}
