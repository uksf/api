using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Teamspeak.Services;

namespace UKSF.Api.Integrations.Teamspeak.EventHandlers;

public interface ITeamspeakEventHandler : IEventHandler { }

public class TeamspeakEventHandler : ITeamspeakEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly IUksfLogger _logger;
    private readonly ITeamspeakService _teamspeakService;

    public TeamspeakEventHandler(IEventBus eventBus, IUksfLogger logger, ITeamspeakService teamspeakService)
    {
        _eventBus = eventBus;
        _logger = logger;
        _teamspeakService = teamspeakService;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<DomainAccount>(HandleAccountEvent, _logger.LogError);
        _eventBus.AsObservable().SubscribeWithAsyncNext<TeamspeakMessageEventData>(HandleTeamspeakMessageEvent, _logger.LogError);
    }

    private async Task HandleAccountEvent(EventModel eventModel, DomainAccount domainAccount)
    {
        await _teamspeakService.UpdateAccountTeamspeakGroups(domainAccount);
    }

    private async Task HandleTeamspeakMessageEvent(EventModel eventModel, TeamspeakMessageEventData messageEvent)
    {
        await _teamspeakService.SendTeamspeakMessageToClient(messageEvent.ClientDbIds, messageEvent.Message);
    }
}
