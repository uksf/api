using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IDevRunsContext : IMongoContext<DomainDevRun>;

public class DevRunsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainDevRun>(mongoCollectionFactory, eventBus, "devRuns"), IDevRunsContext;
