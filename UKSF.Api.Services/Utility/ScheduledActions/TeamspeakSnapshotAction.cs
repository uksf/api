using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility.ScheduledActions {
    public class TeamspeakSnapshotAction : ITeamspeakSnapshotAction {
        public const string ACTION_NAME = nameof(TeamspeakSnapshotAction);

        private readonly ITeamspeakService teamspeakService;

        public TeamspeakSnapshotAction(ITeamspeakService teamspeakService) => this.teamspeakService = teamspeakService;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            teamspeakService.StoreTeamspeakServerSnapshot();
        }
    }
}
