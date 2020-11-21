using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.ScheduledActions {
    public interface IActionPruneBuilds : ISelfCreatingScheduledAction { }

    public class ActionPruneBuilds : IActionPruneBuilds {
        private const string ACTION_NAME = nameof(ActionPruneBuilds);
        private readonly IBuildsContext _buildsContext;

        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;

        public ActionPruneBuilds(IBuildsContext buildsContext, ISchedulerContext schedulerContext, ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock) {
            _buildsContext = buildsContext;
            _schedulerContext = schedulerContext;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            int threshold = _buildsContext.Get(x => x.Environment == GameEnvironment.DEV).Select(x => x.BuildNumber).OrderByDescending(x => x).First() - 100;
            Task modpackBuildsTask = _buildsContext.DeleteMany(x => x.BuildNumber < threshold);

            Task.WaitAll(modpackBuildsTask);
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddDays(1), TimeSpan.FromDays(1), ACTION_NAME);
            }
        }
    }
}
