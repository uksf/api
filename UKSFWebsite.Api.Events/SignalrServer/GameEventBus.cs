using System;
using System.Reactive.Linq;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Events.SignalrServer {
    public class GameEventBus : EventBus<GameEventModel>, IGameEventBus {
        public IObservable<GameEventModel> AsObservable(GameServerType server) => Subject.OfType<GameEventModel>().Where(x => x.server == server);
    }
}
