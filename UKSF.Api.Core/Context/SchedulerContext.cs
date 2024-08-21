using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface ISchedulerContext : IMongoContext<DomainScheduledJob>;

public class SchedulerContext : MongoContext<DomainScheduledJob>, ISchedulerContext
{
    public SchedulerContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "scheduledJobs") { }
}
