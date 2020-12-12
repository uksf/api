using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoreLinq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Discord.Services;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;
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
            serviceProvider.GetService<IDataCacheService>()?.RefreshCachedData();

            // Register scheduled actions & run self-creating scheduled actions
            serviceProvider.GetService<IScheduledActionFactory>()?.RegisterScheduledActions(serviceProvider.GetInterfaceServices<IScheduledAction>());
            serviceProvider.GetInterfaceServices<ISelfCreatingScheduledAction>().ForEach(x => x.CreateSelf());

            // Register build steps
            serviceProvider.GetService<IBuildStepService>()?.RegisterBuildSteps();

            // Add event handlers
            serviceProvider.GetInterfaceServices<IEventHandler>().ForEach(x => x.Init());

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

        public static void StopUksfServices(this IServiceProvider serviceProvider) {
            // Cancel any running builds
            serviceProvider.GetService<IBuildQueueService>()?.CancelAll();

            // Stop teamspeak
            serviceProvider.GetService<ITeamspeakManagerService>()?.Stop();
        }
    }
}
