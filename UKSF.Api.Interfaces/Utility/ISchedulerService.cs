using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Interfaces.Utility {
    public interface ISchedulerService : IDataBackedService<ISchedulerDataService> {
        void Load(bool api = true);
        Task Create(DateTime next, TimeSpan interval, ScheduledJobType type, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }
}
