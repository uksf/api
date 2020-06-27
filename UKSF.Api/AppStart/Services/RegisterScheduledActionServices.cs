using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Utility.ScheduledActions;
using UKSF.Api.Services.Utility.ScheduledActions;

namespace UKSF.Api.AppStart.Services {
    public static class ScheduledActionServiceExtensions {
        public static void RegisterScheduledActionServices(this IServiceCollection services) {
            services.AddTransient<IDeleteExpiredConfirmationCodeAction, DeleteExpiredConfirmationCodeAction>();
            services.AddTransient<IInstagramImagesAction, InstagramImagesAction>();
            services.AddTransient<IPruneLogsAction, PruneLogsAction>();
            services.AddTransient<ITeamspeakSnapshotAction, TeamspeakSnapshotAction>();
        }
    }
}
