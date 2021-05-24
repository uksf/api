using System.Threading.Tasks;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Discord.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Discord.EventHandlers
{
    public interface IDiscordAccountEventHandler : IEventHandler { }

    public class DiscordAccountEventHandler : IDiscordAccountEventHandler
    {
        private readonly IDiscordService _discordService;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;

        public DiscordAccountEventHandler(IEventBus eventBus, ILogger logger, IDiscordService discordService)
        {
            _eventBus = eventBus;
            _logger = logger;
            _discordService = discordService;
        }

        public void Init()
        {
            _eventBus.AsObservable().SubscribeWithAsyncNext<DomainAccount>(HandleAccountEvent, _logger.LogError);
        }

        private async Task HandleAccountEvent(EventModel _, DomainAccount domainAccount)
        {
            await _discordService.UpdateAccount(domainAccount);
        }
    }
}
