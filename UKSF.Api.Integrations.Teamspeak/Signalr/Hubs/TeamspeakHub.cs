using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Teamspeak.Signalr.Clients;

namespace UKSF.Api.Teamspeak.Signalr.Hubs {
    public static class TeamspeakHubState {
        public static bool Connected;
    }

    public class TeamspeakHub : Hub<ITeamspeakClient> {
        public const string END_POINT = "teamspeak";
        private readonly IEventBus<SignalrEventModel> _eventBus;

        public TeamspeakHub(IEventBus<SignalrEventModel> eventBus) => _eventBus = eventBus;

        // ReSharper disable once UnusedMember.Global
        public void Invoke(int procedure, object args) {
            _eventBus.Send(new SignalrEventModel { Procedure = (TeamspeakEventType) procedure, Args = args });
        }

        public override Task OnConnectedAsync() {
            TeamspeakHubState.Connected = true;
            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            TeamspeakHubState.Connected = false;
            await base.OnDisconnectedAsync(exception);
        }
    }
}
