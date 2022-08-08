using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;

namespace UKSF.Api.Teamspeak.Signalr.Hubs;

public static class TeamspeakHubState
{
    public static bool Connected;
}

public class TeamspeakHub : Hub<ITeamspeakClient>
{
    public const string EndPoint = "teamspeak";
    private readonly IEventBus _eventBus;

    public TeamspeakHub(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public void Invoke(int procedure, object args)
    {
        _eventBus.Send(new SignalrEventData { Procedure = (TeamspeakEventType)procedure, Args = args });
    }

    public override Task OnConnectedAsync()
    {
        TeamspeakHubState.Connected = true;
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        TeamspeakHubState.Connected = false;
        await base.OnDisconnectedAsync(exception);
    }
}
