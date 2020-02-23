using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Models.Events.Types;
using UKSF.Common;

namespace UKSF.Api.Signalr.Hubs.Integrations {
    public static class TeamspeakHubState {
        public static bool Connected;
    }

    public class TeamspeakHub : Hub<ITeamspeakClient> {
        public const string END_POINT = "teamspeak";
        private readonly ISignalrEventBus eventBus;

        public TeamspeakHub(ISignalrEventBus eventBus) => this.eventBus = eventBus;

        // ReSharper disable once UnusedMember.Global
        public void Invoke(int procedure, object args) {
            eventBus.Send(EventModelFactory.CreateSignalrEvent((TeamspeakEventType) procedure, args));
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
