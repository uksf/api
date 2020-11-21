using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Services;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.ScheduledActions {
    public interface IActionTeamspeakSnapshot : ISelfCreatingScheduledAction { }

    public class ActionTeamspeakSnapshot : IActionTeamspeakSnapshot {
        private const string ACTION_NAME = nameof(ActionTeamspeakSnapshot);

        private readonly IClock _clock;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly ISchedulerContext _schedulerContext;
        private readonly ISchedulerService _schedulerService;
        private readonly ITeamspeakService _teamspeakService;

        public ActionTeamspeakSnapshot(ISchedulerContext schedulerContext, ITeamspeakService teamspeakService, ISchedulerService schedulerService, IHostEnvironment currentEnvironment, IClock clock) {
            _schedulerContext = schedulerContext;
            _teamspeakService = teamspeakService;
            _schedulerService = schedulerService;
            _currentEnvironment = currentEnvironment;
            _clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            _teamspeakService.StoreTeamspeakServerSnapshot();
        }

        public async Task CreateSelf() {
            if (_currentEnvironment.IsDevelopment()) return;

            if (_schedulerContext.GetSingle(x => x.Action == ACTION_NAME) == null) {
                await _schedulerService.CreateScheduledJob(_clock.Today().AddMinutes(5), TimeSpan.FromMinutes(5), ACTION_NAME);
            }
        }
    }
}
