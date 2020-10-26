using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Utility.Models;

namespace UKSF.Api.Utility.Services.Data {
    public interface ISchedulerDataService : IDataService<ScheduledJob> { }

    public class SchedulerDataService : DataService<ScheduledJob>, ISchedulerDataService {
        public SchedulerDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ScheduledJob> dataEventBus) : base(dataCollectionFactory, dataEventBus, "scheduledJobs") { }
    }
}
