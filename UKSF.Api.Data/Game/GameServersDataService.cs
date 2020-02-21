using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Data.Game {
    public class GameServersDataService : CachedDataService<GameServer, IGameServersDataService>, IGameServersDataService {
        public GameServersDataService(IDataCollection dataCollection, IDataEventBus<IGameServersDataService> dataEventBus) : base(dataCollection, dataEventBus, "gameServers") { }

        public override List<GameServer> Get() {
            return base.Get().OrderBy(x => x.order).ToList();
        }
    }
}
