using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IOpsContext : IMongoContext<DomainOp>;

public class OpsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainOp>(mongoCollectionFactory, eventBus, "ops"), IOpsContext;
