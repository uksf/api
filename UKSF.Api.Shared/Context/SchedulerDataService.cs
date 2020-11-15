using UKSF.Api.Base.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context {
    public interface ISchedulerDataService : IDataService<ScheduledJob> { }

    public class SchedulerDataService : DataService<ScheduledJob>, ISchedulerDataService {
        public SchedulerDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ScheduledJob> dataEventBus) : base(dataCollectionFactory, dataEventBus, "scheduledJobs") { }
    }
}
