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

    public override IEnumerable<Oprep> Get()
    {
        return base.Get().OrderBy(x => x.Start);
    }

    public override IEnumerable<Oprep> Get(Func<Oprep, bool> predicate)
    {
        return base.Get(predicate).OrderBy(x => x.Start);
    }
}
