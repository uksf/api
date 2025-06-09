using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.EventHandlers;

public interface ITeamspeakEventHandler : IEventHandler;

public class TeamspeakEventHandler(IEventBus eventBus, IUksfLogger logger, IAccountContext accountContext, ITeamspeakService teamspeakService)
    : ITeamspeakEventHandler
{
    public void EarlyInit() { }

    public void Init()
    {
        eventBus.AsObservable().SubscribeWithAsyncNext<ContextEventData<DomainAccount>>(HandleAccountEvent, logger.LogError);
        eventBus.AsObservable().SubscribeWithAsyncNext<TeamspeakMessageEventData>(HandleTeamspeakMessageEvent, logger.LogError);
    }

    private async Task HandleAccountEvent(EventModel eventModel, ContextEventData<DomainAccount> contextEventData)
    {
        var account = contextEventData.Data ?? accountContext.GetSingle(contextEventData.Id);
        await teamspeakService.UpdateAccountTeamspeakGroups(account);
    }

    private async Task HandleTeamspeakMessageEvent(EventModel eventModel, TeamspeakMessageEventData messageEvent)
    {
        await teamspeakService.SendTeamspeakMessageToClient(messageEvent.ClientDbIds, messageEvent.Message);
    }
}
