using System.Threading.Tasks;
using UKSF.Api.Integration.Instagram.Services;
using UKSF.Api.Utility.ScheduledActions;

namespace UKSF.Api.Integration.Instagram.ScheduledActions {
    public interface IInstagramImagesAction : IScheduledAction { }

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
