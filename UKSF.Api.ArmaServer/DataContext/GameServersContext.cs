using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IGameServersContext : IMongoContext<DomainGameServer>, ICachedMongoContext;

public class GameServersContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainGameServer>(mongoCollectionFactory, eventBus, variablesService, "gameServers"), IGameServersContext
{
    protected override IEnumerable<DomainGameServer> OrderCollection(IEnumerable<DomainGameServer> collection)
    {
        return collection.OrderBy(x => x.Order);
    }
}
