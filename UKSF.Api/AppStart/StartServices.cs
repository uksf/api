using MoreLinq;
using UKSF.Api.Admin.Services;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.ScheduledActions;
using UKSF.Api.Discord.Services;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Services;
using UKSF.Api.Shared.Services;
using UKSF.Api.Teamspeak.Services;

namespace UKSF.Api.AppStart;

public static class StartServices
{
    public static void StartUksfServices(this IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            // Do any test data setup
            // TestDataSetup.Run(serviceProvider);
        }

        // Early init event handlers
        serviceProvider.GetRequiredService<IEnumerable<IEventHandler>>().ForEach(x => x.EarlyInit());

        // Execute any DB migration
        serviceProvider.GetRequiredService<MigrationUtility>().Migrate();

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

        // Connect discord bot
        serviceProvider.GetRequiredService<IDiscordService>().ConnectDiscord();

        // Start scheduler
        serviceProvider.GetRequiredService<ISchedulerService>().Load();

        // Mark running builds as cancelled & run queued builds
        serviceProvider.GetRequiredService<IBuildsService>().CancelInterruptedBuilds();
        serviceProvider.GetRequiredService<IModpackService>().RunQueuedBuilds();
    }

    public static void StopUksfServices(this IServiceProvider serviceProvider)
    {
        // Cancel any running builds
        serviceProvider.GetRequiredService<IBuildQueueService>().CancelAll();

        // Stop teamspeak
        serviceProvider.GetRequiredService<ITeamspeakManagerService>().Stop();
    }
}
