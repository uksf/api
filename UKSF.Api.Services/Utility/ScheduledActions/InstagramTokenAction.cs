using System.Threading.Tasks;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility.ScheduledActions {
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
