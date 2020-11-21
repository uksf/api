using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface ISchedulerContext : IMongoContext<ScheduledJob> { }

    public class SchedulerContext : MongoContext<ScheduledJob>, ISchedulerContext {
        public SchedulerContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<ScheduledJob> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "scheduledJobs") { }
    }
}
