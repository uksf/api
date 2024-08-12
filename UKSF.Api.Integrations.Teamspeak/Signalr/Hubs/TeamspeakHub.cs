using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Integrations.Teamspeak.Signalr.Clients;

namespace UKSF.Api.Integrations.Teamspeak.Signalr.Hubs;

public static class TeamspeakHubState
{
    public static bool Connected;
}

public class TeamspeakHub(IEventBus eventBus) : Hub<ITeamspeakClient>
{
    public const string EndPoint = "teamspeak";

    public void Invoke(int procedure, object args)
    {
        eventBus.Send(new SignalrEventData { Procedure = (TeamspeakEventType)procedure, Args = args }, nameof(TeamspeakHub));
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
