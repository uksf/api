using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Discord.Services;

namespace UKSF.Api.Integrations.Discord.EventHandlers;

public interface IDiscordAccountEventHandler : IEventHandler;

public class DiscordAccountEventHandler(IEventBus eventBus, IUksfLogger logger, IAccountContext accountContext, IDiscordMembersService discordMembersService)
    : IDiscordAccountEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable()
                .SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(
                    HandleAccountEvent,
                    exception => { logger.LogError("Failed to handle account event in discord", exception); }
                );
    }

    private async Task HandleAccountEvent(EventModel _, ContextEventData<DomainAccount> contextEventData)
    {
        logger.LogInfo($"Discord Account Event Handler, id: {contextEventData.Id}, data: {contextEventData.Data?.Id}");
        var domainAccount = contextEventData.Data ?? accountContext.GetSingle(contextEventData.Id);
        await discordMembersService.UpdateUserByAccount(domainAccount);
    }
}
