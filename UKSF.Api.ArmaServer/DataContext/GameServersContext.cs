using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.ArmaServer.DataContext {
    public interface IGameServersContext : IMongoContext<GameServer>, ICachedMongoContext { }

    public class GameServersContext : CachedMongoContext<GameServer>, IGameServersContext {
        public GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "gameServers") { }

        protected override void SetCache(IEnumerable<GameServer> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Order).ToList();
            }
        }
    }
}
