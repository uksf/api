using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Events.Types;

namespace UKSFWebsite.Api.Signalr.Hubs.Integrations {
    public static class TeamspeakHubState {
        public static bool Connected;
    }

    public class TeamspeakHub : Hub<ITeamspeakClient> {
        public const string END_POINT = "teamspeak";
        private readonly ITeamspeakEventBus eventBus;

        public TeamspeakHub(ITeamspeakEventBus eventBus) => this.eventBus = eventBus;

        // ReSharper disable once UnusedMember.Global
        public void Invoke(int procedure, object args) {
            eventBus.Send(EventModelFactory.CreateTeamspeakEvent((TeamspeakEventType) procedure, args));
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
