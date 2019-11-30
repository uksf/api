using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Signalr.Hubs.Game {
    public class GameClientHub : Hub<IGameClientClient> {
        public const string END_POINT = "gameclient";
        private readonly IGameEventBus eventBus;

        public GameClientHub(IGameEventBus eventBus) => this.eventBus = eventBus;

        // ReSharper disable once UnusedMember.Global
        public void Invoke(int procedure, object args) {
            StringValues steamId = Context.GetHttpContext().Request.Query["uid"];
            eventBus.Send(EventModelFactory.CreateGameEvent(GameServerType.CLIENT, (GameEventType) procedure, args));
        }

        public override async Task OnConnectedAsync() {
            StringValues steamId = Context.GetHttpContext().Request.Query["uid"];
            await Groups.AddToGroupAsync(Context.ConnectionId, steamId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues steamId = Context.GetHttpContext().Request.Query["uid"];
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, steamId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
