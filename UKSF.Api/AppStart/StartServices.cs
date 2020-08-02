using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.AppStart {
    public static class StartServices {
        public static void Start() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            if (serviceProvider.GetService<IHostEnvironment>().IsDevelopment()) {
                // Do any test data setup
                // TestDataSetup.Run(serviceProvider);
            }

            // Execute any DB migration
            serviceProvider.GetService<MigrationUtility>().Migrate();

            // Warm cached data services
            RegisterAndWarmCachedData.Warm();

            // Register scheduled actions
            RegisterScheduledActions.Register();

            // Register build steps
            serviceProvider.GetService<IBuildStepService>().RegisterBuildSteps();

            // Add event handlers
            serviceProvider.GetService<EventHandlerInitialiser>().InitEventHandlers();

            // Start teamspeak manager
            serviceProvider.GetService<ITeamspeakManagerService>().Start();

            // Connect discord bot
            serviceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start scheduler
            serviceProvider.GetService<ISchedulerService>().Load();

            // Mark running builds as cancelled & run queued builds
            serviceProvider.GetService<IBuildsService>().CancelInterruptedBuilds();
            serviceProvider.GetService<IModpackService>().RunQueuedBuilds();
        }
    }
}
