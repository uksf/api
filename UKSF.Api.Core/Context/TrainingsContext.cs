using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ITrainingsContext : IMongoContext<DomainTraining>, ICachedMongoContext;

public class TrainingsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainTraining>(mongoCollectionFactory, eventBus, variablesService, "training"), ITrainingsContext
{
    public override DomainTraining GetSingle(string idOrName)
    {
        return GetSingle(x => x.Id == idOrName || x.Name == idOrName);
    }

    protected override IEnumerable<DomainTraining> OrderCollection(IEnumerable<DomainTraining> collection)
    {
        return collection.OrderBy(x => x.Name);
    }
}
