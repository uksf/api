using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Data.Game {
    public class GameServersDataService : CachedDataService<GameServer, IGameServersDataService>, IGameServersDataService {
        public GameServersDataService(IMongoDatabase database, IDataEventBus<IGameServersDataService> dataEventBus) : base(database, dataEventBus, "gameServers") { }

        public override List<GameServer> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }
    }
}
