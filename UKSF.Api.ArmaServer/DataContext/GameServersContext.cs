using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameServersContext : IMongoContext<GameServer>, ICachedMongoContext { }

public class GameServersContext : CachedMongoContext<GameServer>, IGameServersContext
{
    public GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "gameServers") { }

    protected override void SetCache(IEnumerable<GameServer> newCollection)
    {
        lock (LockObject)
        {
            Cache = newCollection?.OrderBy(x => x.Order).ToList();
        }
    }
}
