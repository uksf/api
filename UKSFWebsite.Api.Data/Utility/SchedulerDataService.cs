using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Data.Utility {
    public class SchedulerDataService : DataService<ScheduledJob>, ISchedulerDataService {
        public SchedulerDataService(IMongoDatabase database) : base(database, "scheduledJobs") { }
    }
}
