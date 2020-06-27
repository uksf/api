using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Interfaces.Utility {
    public interface ISchedulerService : IDataBackedService<ISchedulerDataService> {
        void Load();
        Task CreateAndSchedule(DateTime next, TimeSpan interval, string action, params object[] actionParameters);
        Task Cancel(Func<ScheduledJob, bool> predicate);
    }
}
