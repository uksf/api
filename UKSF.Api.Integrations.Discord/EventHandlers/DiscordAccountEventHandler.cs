using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord.EventHandlers;

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
        await _discordMembersService.UpdateUserByAccount(domainAccount);
    }
}
