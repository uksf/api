using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface ISchedulerContext : IMongoContext<ScheduledJob> { }

    public class SchedulerContext : MongoContext<ScheduledJob>, ISchedulerContext {
        public SchedulerContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "scheduledJobs") { }
    }
}
