using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Interfaces.Utility {
    public interface ISchedulerService : IDataBackedService<ISchedulerDataService> {
        void Load(bool integration = false);
        Task Create(DateTime next, TimeSpan interval, ScheduledJobType type, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }
}
