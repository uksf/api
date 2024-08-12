using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ILoaContext : IMongoContext<DomainLoa>, ICachedMongoContext;

public class LoaContext : CachedMongoContext<DomainLoa>, ILoaContext
{
    public LoaContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "loas"
    ) { }
}
