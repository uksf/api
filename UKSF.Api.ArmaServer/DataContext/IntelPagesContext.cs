using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IIntelPagesContext : IMongoContext<DomainIntelPage>;

public class IntelPagesContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainIntelPage>(mongoCollectionFactory, eventBus, "intelPages"), IIntelPagesContext;
