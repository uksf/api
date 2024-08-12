using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameServersContext : IMongoContext<GameServer>, ICachedMongoContext;

public class GameServersContext : CachedMongoContext<GameServer>, IGameServersContext
{
    public GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "gameServers"
    ) { }

    protected override IEnumerable<GameServer> OrderCollection(IEnumerable<GameServer> collection)
    {
        return collection.OrderBy(x => x.Order);
    }
}
