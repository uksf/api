using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.ArmaServer.DataContext {
    public interface IGameServersContext : IMongoContext<GameServer>, ICachedMongoContext { }

    public class GameServersContext : CachedMongoContext<GameServer>, IGameServersContext {
        public GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<GameServer> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "gameServers") { }

        protected override void SetCache(IEnumerable<GameServer> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.Order).ToList();
            }
        }
    }
}
