using UKSF.Api.Discord.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Discord.EventHandlers;

public interface IDiscordAccountEventHandler : IEventHandler { }

public class DiscordAccountEventHandler : IDiscordAccountEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;
    private readonly IDiscordMembersService _discordMembersService;

    public DiscordAccountEventHandler(IEventBus eventBus, IUksfLogger logger, IDiscordMembersService discordMembersService)
    {
        _eventBus = eventBus;
        _logger = logger;
        _discordMembersService = discordMembersService;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<DomainAccount>(HandleAccountEvent, _logger.LogError);
    }

    private async Task HandleAccountEvent(EventModel _, DomainAccount domainAccount)
    {
        await _discordMembersService.UpdateAccount(domainAccount);
    }
}
