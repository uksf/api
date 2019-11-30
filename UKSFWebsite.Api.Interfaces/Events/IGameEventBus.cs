using System;
using UKSFWebsite.Api.Models.Events;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Interfaces.Events {
    public interface IGameEventBus : IEventBus<GameEventModel> {
        IObservable<GameEventModel> AsObservable(GameServerType server);
    }
}
