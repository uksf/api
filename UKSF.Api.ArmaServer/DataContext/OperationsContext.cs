using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IOperationsContext : IMongoContext<DomainOperation>, ICachedMongoContext;

public class OperationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainOperation>(mongoCollectionFactory, eventBus, variablesService, "operations"), IOperationsContext;
