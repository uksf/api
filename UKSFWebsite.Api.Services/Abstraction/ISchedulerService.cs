using System;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ISchedulerService : IDataService<ScheduledJob> {
        void Load(bool integration = false);
        Task Create(DateTime next, TimeSpan interval, ScheduledJobType type, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }
}