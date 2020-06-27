using System.Threading.Tasks;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Utility.ScheduledActions;

namespace UKSF.Api.Services.Utility.ScheduledActions {
    public class InstagramImagesAction : IInstagramImagesAction {
        public const string ACTION_NAME = nameof(InstagramImagesAction);

        private readonly IInstagramService instagramService;

        public InstagramImagesAction(IInstagramService instagramService) => this.instagramService = instagramService;

        public string Name => ACTION_NAME;

        public void Run(params object[] parameters) {
            Task unused = instagramService.CacheInstagramImages();
        }
    }
}
