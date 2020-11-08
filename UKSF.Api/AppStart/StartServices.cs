using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoreLinq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Base.Services;
using UKSF.Api.Discord.Services;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Services;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.AppStart {
    public static class StartServices {
        public static void StartUksfServices(this IServiceProvider serviceProvider) {
            if (serviceProvider.GetService<IHostEnvironment>().IsDevelopment()) {
                // Do any test data setup
                // TestDataSetup.Run(serviceProvider);
            }

            // Execute any DB migration
            serviceProvider.GetService<MigrationUtility>()?.Migrate();

            // Warm cached data services
            serviceProvider.GetService<IDataCacheService>()?.InvalidateCachedData();

            // Register scheduled actions & run self-creating scheduled actions
            serviceProvider.GetService<IScheduledActionFactory>()?.RegisterScheduledActions(serviceProvider.GetServices<IScheduledAction>());
            serviceProvider.GetServices<ISelfCreatingScheduledAction>().ForEach(x => x.CreateSelf());

            // Register build steps
            serviceProvider.GetService<IBuildStepService>()?.RegisterBuildSteps();

            // Add event handlers
            serviceProvider.GetServices<IEventHandler>().ForEach(x => x.Init());

            // Start teamspeak manager
            serviceProvider.GetService<ITeamspeakManagerService>()?.Start();

            // Connect discord bot
            serviceProvider.GetService<IDiscordService>()?.ConnectDiscord();

            // Start scheduler
            serviceProvider.GetService<ISchedulerService>()?.Load();

            // Mark running builds as cancelled & run queued builds
            serviceProvider.GetService<IBuildsService>()?.CancelInterruptedBuilds();
            serviceProvider.GetService<IModpackService>()?.RunQueuedBuilds();
        }
    }
}
