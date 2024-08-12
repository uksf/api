using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface ISchedulerContext : IMongoContext<ScheduledJob>;

public class SchedulerContext : MongoContext<ScheduledJob>, ISchedulerContext
{
    public SchedulerContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "scheduledJobs") { }
}
