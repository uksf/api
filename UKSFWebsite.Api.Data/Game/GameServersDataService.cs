using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Game;

namespace UKSFWebsite.Api.Data.Game {
    public class GameServersDataService : CachedDataService<GameServer>, IGameServersDataService {
        public GameServersDataService(IMongoDatabase database) : base(database, "gameServers") { }

        public override List<GameServer> Get() {
            base.Get();
            Collection = Collection.OrderBy(x => x.order).ToList();
            return Collection;
        }
    }
}
