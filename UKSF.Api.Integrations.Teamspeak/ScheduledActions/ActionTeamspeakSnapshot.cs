using System;
using System.Threading.Tasks;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.Teamspeak.ScheduledActions {
    public interface IActionTeamspeakSnapshot : ISelfCreatingScheduledAction { }

    public class ActionTeamspeakSnapshot : IActionTeamspeakSnapshot {
        public const string ACTION_NAME = nameof(ActionTeamspeakSnapshot);

        private readonly IClock clock;
        private readonly ISchedulerService schedulerService;
        private readonly ITeamspeakService teamspeakService;

        public ActionTeamspeakSnapshot(ITeamspeakService teamspeakService, ISchedulerService schedulerService, IClock clock) {
            this.teamspeakService = teamspeakService;
            this.schedulerService = schedulerService;
            this.clock = clock;
        }

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            teamspeakService.StoreTeamspeakServerSnapshot();
        }

        public async Task CreateSelf() {
            if (schedulerService.Data.GetSingle(x => x.action == ACTION_NAME) == null) {
                await schedulerService.CreateScheduledJob(clock.Today().AddMinutes(5), TimeSpan.FromMinutes(5), ACTION_NAME);
            }
        }
    }
}
