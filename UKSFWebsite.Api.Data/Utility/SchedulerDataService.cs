using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Data.Utility {
    public class SchedulerDataService : DataService<ScheduledJob, ISchedulerDataService>, ISchedulerDataService {
        public SchedulerDataService(IMongoDatabase database, IDataEventBus<ISchedulerDataService> dataEventBus) : base(database, dataEventBus, "scheduledJobs") { }
    }
}
