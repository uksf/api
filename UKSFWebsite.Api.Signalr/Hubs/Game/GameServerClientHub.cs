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
    public class GameServerClientHub : Hub<IGameServerClient> {
        public const string END_POINT = "gameserver";
        private readonly IGameEventBus eventBus;

        public GameServerClientHub(IGameEventBus eventBus) => this.eventBus = eventBus;

        // ReSharper disable once UnusedMember.Global
        public void Invoke(int procedure, object args) {
            StringValues type = Context.GetHttpContext().Request.Query["type"];
            eventBus.Send(EventModelFactory.CreateGameEvent(type == "0" ? GameServerType.SERVER : GameServerType.HEADLESS, (GameEventType) procedure, args));
        }

        public override async Task OnConnectedAsync() {
            StringValues port = Context.GetHttpContext().Request.Query["port"];
            StringValues type = Context.GetHttpContext().Request.Query["type"];
            StringValues name = Context.GetHttpContext().Request.Query["name"];
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{port}:{type}:{name}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception) {
            StringValues port = Context.GetHttpContext().Request.Query["port"];
            StringValues type = Context.GetHttpContext().Request.Query["type"];
            StringValues name = Context.GetHttpContext().Request.Query["name"];
            eventBus.Send(EventModelFactory.CreateGameEvent(GameServerType.SERVER, GameEventType.REMOVE_SERVER_STATUS, $"{port}:{type}:{name}"));
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{port}:{type}:{name}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
