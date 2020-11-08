using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Base.Context {
    public interface ISchedulerDataService : IDataService<ScheduledJob> { }

    public class SchedulerDataService : DataService<ScheduledJob>, ISchedulerDataService {
        public SchedulerDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ScheduledJob> dataEventBus) : base(dataCollectionFactory, dataEventBus, "scheduledJobs") { }
    }
}
