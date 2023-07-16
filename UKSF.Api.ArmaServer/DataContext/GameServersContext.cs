using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameServersContext : IMongoContext<GameServer>, ICachedMongoContext { }

public class GameServersContext : CachedMongoContext<GameServer>, IGameServersContext
{
    public GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "gameServers"
    ) { }

    public override IEnumerable<GameServer> Get()
    {
        return base.Get().OrderBy(x => x.Order);
    }

    public override IEnumerable<GameServer> Get(Func<GameServer, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Order);
    }
}
