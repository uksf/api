using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IApplicationFunnelEventContext : IMongoContext<DomainApplicationFunnelEvent>;

public class ApplicationFunnelEventContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainApplicationFunnelEvent>(mongoCollectionFactory, eventBus, "applicationFunnelEvents"), IApplicationFunnelEventContext;
