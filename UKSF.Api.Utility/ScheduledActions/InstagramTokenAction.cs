using System.Threading.Tasks;

namespace UKSF.Api.Utility.ScheduledActions {
    public interface IInstagramTokenAction : IScheduledAction { }

    public class InstagramTokenAction : IInstagramTokenAction {
        public const string ACTION_NAME = nameof(InstagramTokenAction);

        private readonly IInstagramService instagramService;

        public InstagramTokenAction(IInstagramService instagramService) => this.instagramService = instagramService;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = instagramService.RefreshAccessToken();
        }
    }
}
