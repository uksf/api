using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.EventHandlers;

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
