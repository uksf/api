using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.ScheduledActions;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Signalr.Hubs;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack;

public static class ApiModpackExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfModpack()
        {
            services.AddHttpClient(
                "Steam",
                client =>
                {
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36"
                    );
                }
            );

            return services.AddContexts().AddEventHandlers().AddServices().AddActions();
        }

        private IServiceCollection AddContexts()
        {
            return services.AddCachedContext<IBuildsContext, BuildsContext>()
                           .AddCachedContext<IReleasesContext, ReleasesContext>()
                           .AddCachedContext<IWorkshopModsContext, WorkshopModsContext>();
        }

        private IServiceCollection AddEventHandlers()
        {
            return services.AddEventHandler<IBuildsEventHandler, BuildsEventHandler>()
                           .AddEventHandler<IWorkshopModDataEventHandler, WorkshopModDataEventHandler>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<IBuildsService, BuildsService>()
                           .AddSingleton<IGithubClientService, GithubClientService>()
                           .AddTransient<IGithubService, GithubService>()
                           .AddSingleton<IGithubIssuesService, GithubIssuesService>()
                           .AddTransient<IModpackService, ModpackService>()
                           .AddTransient<IReleaseService, ReleaseService>()
                           .AddSingleton<IBuildStepService, BuildStepService>()
                           .AddSingleton<IBuildProcessorService, BuildProcessorService>()
                           .AddSingleton<IBuildQueueService, BuildQueueService>()
                           .AddSingleton<IVersionService, VersionService>()
                           .AddSingleton<IBuildProcessTracker, BuildProcessTracker>()
                           .AddTransient<IWorkshopModsService, WorkshopModsService>()
                           .AddSingleton<ISteamApiService, SteamApiService>()
                           .AddTransient<IWorkshopModsProcessingService, WorkshopModsProcessingService>()
                           .AddTransient<IInstallOperation, InstallOperation>()
                           .AddTransient<IUpdateOperation, UpdateOperation>()
                           .AddTransient<IUninstallOperation, UninstallOperation>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionPruneBuilds, ActionPruneBuilds>()
                           .AddSelfCreatingScheduledAction<IActionRefreshSteamToken, ActionRefreshSteamToken>();
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfModpackSignalr()
        {
            builder.MapHub<ModpackHub>($"/hub/{ModpackHub.EndPoint}");
        }
    }
}
