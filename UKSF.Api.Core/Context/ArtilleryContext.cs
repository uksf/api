using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IArtilleryContext : IMongoContext<DomainArtillery>, ICachedMongoContext;

public class ArtilleryContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainArtillery>(mongoCollectionFactory, eventBus, variablesService, "artillery"), IArtilleryContext
{
    protected override IEnumerable<DomainArtillery> OrderCollection(IEnumerable<DomainArtillery> collection)
    {
        return collection.OrderBy(x => x.Key);
    }

    public override DomainArtillery GetSingle(string idOrKey)
    {
        return GetSingle(x => x.Id == idOrKey || x.Key == idOrKey);
    }
}
