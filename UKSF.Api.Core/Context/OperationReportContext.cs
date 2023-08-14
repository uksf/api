using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IOperationReportContext : IMongoContext<Oprep>, ICachedMongoContext { }

public class OperationReportContext : CachedMongoContext<Oprep>, IOperationReportContext
{
    public OperationReportContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "oprep"
    ) { }

    protected override IEnumerable<Oprep> OrderCollection(IEnumerable<Oprep> collection)
    {
        return collection.OrderBy(x => x.Start);
    }
}
