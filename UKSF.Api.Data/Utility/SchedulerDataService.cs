using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Data.Utility {
    public class SchedulerDataService : DataService<ScheduledJob, ISchedulerDataService>, ISchedulerDataService {
        public SchedulerDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ISchedulerDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "scheduledJobs") { }
    }
}
