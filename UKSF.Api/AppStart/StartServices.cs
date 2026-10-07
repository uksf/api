using MoreLinq;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.EventHandlers;
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
            serviceProvider.GetRequiredService<IEnumerable<IEventHandler>>().ForEach(x => x.EarlyInit());
            serviceProvider.RunStartupMigrations();
            serviceProvider.GetRequiredService<IDataCacheService>().RefreshCachedData();

            serviceProvider.GetRequiredService<IScheduledActionFactory>()
                           .RegisterScheduledActions(serviceProvider.GetRequiredService<IEnumerable<IScheduledAction>>());
            serviceProvider.CreateSelfScheduledJobs();

            serviceProvider.GetRequiredService<IBuildStepService>().RegisterBuildSteps();
            serviceProvider.GetRequiredService<IEnumerable<IEventHandler>>().ForEach(x => x.Init());

            serviceProvider.StartIntegrations();
        }

        public void RunStartupMigrations()
        {
            if (serviceProvider.GetRequiredService<VerifyMode>().Enabled)
            {
                Console.Out.WriteLine("verify mode: database migrations are not run");
                return;
            }

            serviceProvider.GetRequiredService<MigrationUtility>().RunMigrations().Wait(TimeSpan.FromMinutes(5));
        }

        public void CreateSelfScheduledJobs()
        {
            if (serviceProvider.GetRequiredService<VerifyMode>().Enabled)
            {
                return;
            }

            serviceProvider.GetRequiredService<IEnumerable<ISelfCreatingScheduledAction>>().ForEach(x => x.CreateSelf());
        }

        public void StartIntegrations()
        {
            if (serviceProvider.GetRequiredService<VerifyMode>().Enabled)
            {
                Console.Out.WriteLine("verify mode: Teamspeak, Discord, the scheduler and queued builds are not started");
                return;
            }

            serviceProvider.GetRequiredService<ITeamspeakManagerService>().Start();
            serviceProvider.GetRequiredService<IDiscordActivationService>().Activate();
            serviceProvider.GetRequiredService<ISchedulerService>().Load();

            serviceProvider.GetRequiredService<IBuildsService>().CancelInterruptedBuilds().Wait(TimeSpan.FromSeconds(30));
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IModpackService>().RunQueuedBuilds();
        }

        public void StopUksfServices()
        {
            serviceProvider.GetRequiredService<IBuildQueueService>().CancelAll().Wait(TimeSpan.FromSeconds(30));
            Console.Out.WriteLine("stopped builds");

            serviceProvider.StopIntegrations();

            serviceProvider.GetRequiredService<IUksfLoggerEventHandler>().FlushAsync().Wait(TimeSpan.FromSeconds(10));
            Console.Out.WriteLine("flushed logs");
        }

        public void StopIntegrations()
        {
            if (serviceProvider.GetRequiredService<VerifyMode>().Enabled)
            {
                return;
            }

            serviceProvider.GetRequiredService<ITeamspeakManagerService>().Stop();
            Console.Out.WriteLine("stopped ts");

            serviceProvider.GetRequiredService<IDiscordActivationService>().Deactivate().Wait(TimeSpan.FromSeconds(5));
            Console.Out.WriteLine("stopped discord");
        }
    }
}
