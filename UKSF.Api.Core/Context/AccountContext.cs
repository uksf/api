using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IAccountContext : IMongoContext<DomainAccount>, ICachedMongoContext;

public class AccountContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainAccount>(mongoCollectionFactory, eventBus, variablesService, "accounts"), IAccountContext;
