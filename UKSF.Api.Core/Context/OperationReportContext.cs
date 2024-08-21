using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IOperationReportContext : IMongoContext<DomainOprep>, ICachedMongoContext;

public class OperationReportContext : CachedMongoContext<DomainOprep>, IOperationReportContext
{
    public OperationReportContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "oprep"
    ) { }

    protected override IEnumerable<DomainOprep> OrderCollection(IEnumerable<DomainOprep> collection)
    {
        return collection.OrderBy(x => x.Start);
    }
}
