using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.ScheduledActions {
    public interface IActionPruneBuilds : ISelfCreatingScheduledAction { }

    public class ActionPruneBuilds : IActionPruneBuilds {
        private const string ACTION_NAME = nameof(ActionPruneBuilds);

        private readonly IClock _clock;
        private readonly IDataCollectionFactory _dataCollectionFactory;
        private readonly ISchedulerService _schedulerService;
        private readonly IHostEnvironment _currentEnvironment;

        public ActionPruneBuilds(IDataCollectionFactory dataCollectionFactory, ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock) {
            _dataCollectionFactory = dataCollectionFactory;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            IDataCollection<ModpackBuild> buildsData = _dataCollectionFactory.CreateDataCollection<ModpackBuild>("modpackBuilds");
            int threshold = buildsData.Get(x => x.Environment == GameEnvironment.DEV).Select(x => x.BuildNumber).OrderByDescending(x => x).First() - 100;
            Task modpackBuildsTask = buildsData.DeleteManyAsync(x => x.BuildNumber < threshold);

            Task.WaitAll(modpackBuildsTask);
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
