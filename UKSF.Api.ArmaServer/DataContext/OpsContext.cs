using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IOpsContext : IMongoContext<DomainOp>, ICachedMongoContext;

public class OpsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainOp>(mongoCollectionFactory, eventBus, variablesService, "ops"), IOpsContext;
