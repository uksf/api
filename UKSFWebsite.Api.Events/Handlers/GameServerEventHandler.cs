using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Game;
using UKSFWebsite.Api.Interfaces.Hubs;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Events.Types;
using UKSFWebsite.Api.Models.Game;
using UKSFWebsite.Api.Signalr.Hubs.Game;

namespace UKSFWebsite.Api.Events.Handlers {
    public class GameServerEventHandler : IGameServerEventHandler {
        private readonly IGameEventBus eventBus;
        private readonly IHubContext<GameServersHub, IGameServersClient> hub;
        private readonly IGameServersService gameServersService;

        public GameServerEventHandler(IGameEventBus eventBus, IHubContext<GameServersHub, IGameServersClient> hub, IGameServersService gameServersService) {
            this.eventBus = eventBus;
            this.hub = hub;
            this.gameServersService = gameServersService;
        }

        public void Init() {
            eventBus.AsObservable(GameServerType.SERVER)
                    .Subscribe(
                        async x => { await HandleEvent(x); }
                    );
        }

        private async Task HandleEvent(GameEventModel eventModel) {
            string args = eventModel.args.ToString();
            switch (eventModel.procedure) {
                case GameEventType.UPDATE_SERVER_STATUS:
                    await UpdateServerStatus(args);
                    break;
                case GameEventType.REMOVE_SERVER_STATUS:
                    await RemoveServerStatus(args);
                    break;
                case GameEventType.SAFE_SHUTDOWN:
                    await SafeShutdown(args);
                    break;
                case GameEventType.EMPTY: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private async Task UpdateServerStatus(string status) {
            Console.Out.WriteLine(status);
            GameServerStatus gameServerStatus = JsonConvert.DeserializeObject<GameServerStatus>(status);
            await gameServersService.UpdateGameServerStatus(gameServerStatus);
        }

        private async Task RemoveServerStatus(string key) {
            Console.Out.WriteLine($"{key} disconnected, removing");
            await gameServersService.RemoveGameServerStatus(key);
        }

        private async Task SafeShutdown(string name) {
            Console.Out.WriteLine($"{name} requested shutdown");
            GameServer gameServer = gameServersService.Data().GetSingle(x => x.name == name);
            await gameServersService.StopGameServer(gameServer);
        }
    }
}
