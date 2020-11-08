using System;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.ScheduledActions {
    public interface IActionPruneBuilds : ISelfCreatingScheduledAction { }

    public class ActionPruneBuilds : IActionPruneBuilds {
        public const string ACTION_NAME = nameof(ActionPruneBuilds);

        private readonly IClock _clock;
        private readonly IDataCollectionFactory _dataCollectionFactory;
        private readonly ISchedulerService _schedulerService;

        public ActionPruneBuilds(IDataCollectionFactory dataCollectionFactory, ISchedulerService schedulerService, IClock clock) {
            _dataCollectionFactory = dataCollectionFactory;
            _schedulerService = schedulerService;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            DateTime now = DateTime.Now;

            IDataCollection<ModpackBuild> buildsData = _dataCollectionFactory.CreateDataCollection<ModpackBuild>("modpackBuilds");
            int threshold = buildsData.Get(x => x.Environment == GameEnvironment.DEV).Select(x => x.BuildNumber).OrderByDescending(x => x).First() - 100;
            Task modpackBuildsTask = buildsData.DeleteManyAsync(x => x.BuildNumber < threshold);

            Task.WaitAll(modpackBuildsTask);
        }

        public async Task CreateSelf() {
            if (_schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
