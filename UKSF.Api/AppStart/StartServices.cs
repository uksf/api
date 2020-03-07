using System;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.AppStart {
    public static class StartServices {
        public static void Start() {
            IServiceProvider serviceProvider = Global.ServiceProvider;

            // Execute any DB migration
            serviceProvider.GetService<MigrationUtility>().Migrate();

            // Warm cached data services
            RegisterAndWarmCachedData.Warm();

            // Register scheduled actions
            RegisterScheduledActions.Register();

            // Add event handlers
            serviceProvider.GetService<EventHandlerInitialiser>().InitEventHandlers();

            // Start teamspeak manager
            serviceProvider.GetService<ITeamspeakManagerService>().Start();

            // Connect discord bot
            serviceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start scheduler
            serviceProvider.GetService<ISchedulerService>().LoadApi();
        }
    }
}
