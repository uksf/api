using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoreLinq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Services;
using UKSF.Api.Utility.ScheduledActions;
using UKSF.Api.Utility.Services;

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

            // Register scheduled actions
            serviceProvider.GetService<IScheduledActionService>()?.RegisterScheduledActions(serviceProvider.GetServices<IScheduledAction>());

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
