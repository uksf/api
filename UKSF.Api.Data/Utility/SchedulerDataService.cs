using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Data.Utility {
    public class SchedulerDataService : DataService<ScheduledJob, ISchedulerDataService>, ISchedulerDataService {
        public SchedulerDataService(IDataCollection dataCollection, IDataEventBus<ISchedulerDataService> dataEventBus) : base(dataCollection, dataEventBus, "scheduledJobs") { }
    }
}
