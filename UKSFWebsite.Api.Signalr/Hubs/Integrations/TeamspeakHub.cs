using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Events.Types;
// ReSharper disable UnusedMember.Global

namespace UKSFWebsite.Api.Signalr.Hubs.Integrations {
    public static class TeamspeakHubState {
        public static bool Connected;
    }
    
    public class TeamspeakHub : Hub<ITeamspeakClient> {
        private readonly ISignalrEventBus eventBus;
        public const string END_POINT = "teamspeak";

        public TeamspeakHub(ISignalrEventBus eventBus) => this.eventBus = eventBus;

        public void Invoke(int procedure, object args) {
            eventBus.Send(EventModelFactory.CreateSignalrEvent((TeamspeakEventType)procedure, args));
        }

        public override Task OnConnectedAsync() {
            TeamspeakHubState.Connected = true;
            return base.OnConnectedAsync();
        }
        
        public override Task OnDisconnectedAsync(Exception exception) {
            TeamspeakHubState.Connected = false;
            return base.OnDisconnectedAsync(exception);
        }
    }
}
