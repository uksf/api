using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.ArmaServer.DataContext {
    public interface IGameServersDataService : IDataService<GameServer>, ICachedDataService { }

    public class GameServersDataService : CachedDataService<GameServer>, IGameServersDataService {
        public GameServersDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<GameServer> dataEventBus) : base(dataCollectionFactory, dataEventBus, "gameServers") { }

        protected override void SetCache(IEnumerable<GameServer> newCollection) {
            lock (LockObject) {
                Cache = newCollection?.OrderBy(x => x.order).ToList();
            }
        }
    }
}
