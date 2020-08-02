using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Data.Game {
    public class GameServersDataService : CachedDataService<GameServer, IGameServersDataService>, IGameServersDataService {
        public GameServersDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<IGameServersDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "gameServers") { }

        public override List<GameServer> Collection {
            get => base.Collection;
            protected set {
                lock (LockObject) base.Collection = value?.OrderBy(x => x.order).ToList();
            }
        }
    }
}
