using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Utility.Models;
using UKSF.Api.Utility.ScheduledActions;
using UKSF.Api.Utility.Services;
using UKSF.Api.Utility.Services.Data;

namespace UKSF.Api.Utility {
    public static class ApiUtilityExtensions {

        public static IServiceCollection AddUksfUtility(this IServiceCollection services, IConfiguration configuration) {

            services.AddTransient<ISchedulerDataService, SchedulerDataService>();
            services.AddTransient<ISchedulerService, SchedulerService>();
            services.AddSingleton<IScheduledActionService, ScheduledActionService>();

            services.AddSingleton<IDataEventBus<ScheduledJob>, DataEventBus<ScheduledJob>>();

            services.AddTransient<IInstagramImagesAction, InstagramImagesAction>();
            services.AddTransient<IInstagramTokenAction, InstagramTokenAction>();
            services.AddTransient<IPruneDataAction, PruneDataAction>();
            services.AddTransient<ITeamspeakSnapshotAction, TeamspeakSnapshotAction>();

            return services;
        }
    }
}
