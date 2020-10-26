namespace UKSF.Api.Utility.ScheduledActions {
    public interface ITeamspeakSnapshotAction : IScheduledAction { }

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
