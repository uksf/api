using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Data.Game {
    public class GameServersDataService : CachedDataService<GameServer>, IGameServersDataService {
        public GameServersDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<GameServer> dataEventBus) : base(dataCollectionFactory, dataEventBus, "gameServers") { }

        protected override void SetCache(IEnumerable<GameServer> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.order).ToList();
            }
        }
    }
}
