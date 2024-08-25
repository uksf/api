using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ILoaContext : IMongoContext<DomainLoa>, ICachedMongoContext;

public class LoaContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainLoa>(mongoCollectionFactory, eventBus, variablesService, "loas"), ILoaContext;
