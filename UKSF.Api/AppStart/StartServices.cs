using MoreLinq;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Discord.Services;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Services;

namespace UKSF.Api.AppStart;

public static class StartServices
{
    extension(IServiceProvider serviceProvider)
    {
        public void StartUksfServices()
        {
            if (serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                // Do any test data setup
                // TestDataSetup.Run(serviceProvider);
            }

            // Early init event handlers
            serviceProvider.GetRequiredService<IEnumerable<IEventHandler>>().ForEach(x => x.EarlyInit());

            // Execute any DB migration
            serviceProvider.GetRequiredService<MigrationUtility>().RunMigrations().Wait(TimeSpan.FromMinutes(5));

            // Warm cached data services
            serviceProvider.GetRequiredService<IDataCacheService>().RefreshCachedData();

            // Register scheduled actions & run self-creating scheduled actions
            serviceProvider.GetRequiredService<IScheduledActionFactory>()
                           .RegisterScheduledActions(serviceProvider.GetRequiredService<IEnumerable<IScheduledAction>>());
            serviceProvider.GetRequiredService<IEnumerable<ISelfCreatingScheduledAction>>().ForEach(x => x.CreateSelf());

            // Register build steps
            serviceProvider.GetRequiredService<IBuildStepService>().RegisterBuildSteps();

            // Init event handlers
            serviceProvider.GetRequiredService<IEnumerable<IEventHandler>>().ForEach(x => x.Init());

            // Start teamspeak manager
            serviceProvider.GetRequiredService<ITeamspeakManagerService>().Start();

            // Initialise discord bot
            serviceProvider.GetRequiredService<IDiscordActivationService>().Activate();

            // Start scheduler
            serviceProvider.GetRequiredService<ISchedulerService>().Load();

            // Mark running builds as cancelled & run queued builds
            serviceProvider.GetRequiredService<IBuildsService>().CancelInterruptedBuilds().Wait(TimeSpan.FromSeconds(30));
            serviceProvider.GetRequiredService<IModpackService>().RunQueuedBuilds();
        }

        public void StopUksfServices()
        {
            // Cancel any running builds in the queue
            serviceProvider.GetRequiredService<IBuildQueueService>().CancelAll().Wait(TimeSpan.FromSeconds(30));
            Console.Out.WriteLine("stopped builds");

            // Stop teamspeak
            serviceProvider.GetRequiredService<ITeamspeakManagerService>().Stop();
            Console.Out.WriteLine("stopped ts");

            // Stop discord
            serviceProvider.GetRequiredService<IDiscordActivationService>().Deactivate().Wait(TimeSpan.FromSeconds(5));
            Console.Out.WriteLine("stopped discord");
        }
    }
}
