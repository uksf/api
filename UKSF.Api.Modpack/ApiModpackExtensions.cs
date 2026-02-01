using MassTransit;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.ScheduledActions;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Signalr.Hubs;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;

namespace UKSF.Api.Modpack;

public static class ApiModpackExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfModpack()
        {
            services.AddMassTransit(x =>
                {
                    x.AddConsumer<WorkshopModInstallDownloadConsumer>();
                    x.AddConsumer<WorkshopModUpdateDownloadConsumer>();
                    x.AddConsumer<WorkshopModInstallCheckConsumer>();
                    x.AddConsumer<WorkshopModUpdateCheckConsumer>();
                    x.AddConsumer<WorkshopModInstallConsumer>();
                    x.AddConsumer<WorkshopModUpdateConsumer>();
                    x.AddConsumer<WorkshopModUninstallConsumer>();
                    x.AddConsumer<WorkshopModCleanupConsumer>();

                    x.AddSagaStateMachine<WorkshopModStateMachine, WorkshopModInstanceState>()
                     .MongoDbRepository(r =>
                         {
                             var appSettings = services.BuildServiceProvider().GetRequiredService<IOptions<AppSettings>>().Value;
                             r.Connection = appSettings.ConnectionStrings.Database;
                             r.DatabaseName = MongoUrl.Create(appSettings.ConnectionStrings.Database).DatabaseName;
                             r.CollectionName = "workshopModSagas";
                         }
                     );

                    x.UsingInMemory((context, cfg) =>
                        {
                            cfg.ConfigureEndpoints(context);
                            cfg.UseInMemoryOutbox(context);
                        }
                    );

                    x.Configure<MassTransitHostOptions>(options =>
                        {
                            options.StopTimeout = TimeSpan.FromSeconds(5);
                            options.ConsumerStopTimeout = TimeSpan.FromSeconds(2);
                        }
                    );
                }
            );

            return services.AddContexts().AddEventHandlers().AddServices().AddActions().AddTransient<IBuildsEventHandler, BuildsEventHandler>();
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
                           .AddScoped<IWorkshopModsService, WorkshopModsService>()
                           .AddSingleton<ISteamApiService, SteamApiService>()
                           .AddSingleton<IWorkshopModsProcessingService, WorkshopModsProcessingService>()
                           .AddTransient<IInstallOperation, InstallOperation>()
                           .AddTransient<IUpdateOperation, UpdateOperation>()
                           .AddTransient<IUninstallOperation, UninstallOperation>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionPruneBuilds, ActionPruneBuilds>();
        }
    }

    public static void AddUksfModpackSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<ModpackHub>($"/hub/{ModpackHub.EndPoint}");
    }
}
