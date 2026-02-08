using MassTransit;
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
        public IServiceCollection AddUksfModpack(IConfiguration configuration)
        {
            var appSettings = new AppSettings();
            configuration.GetSection(nameof(AppSettings)).Bind(appSettings);

            services.AddMassTransit(x =>
                {
                    x.AddConsumer<WorkshopModDownloadConsumer>();
                    x.AddConsumer<WorkshopModCheckConsumer>();
                    x.AddConsumer<WorkshopModExecuteConsumer>();
                    x.AddConsumer<WorkshopModUninstallConsumer>();
                    x.AddConsumer<WorkshopModCleanupConsumer>();

                    x.AddSagaStateMachine<WorkshopModStateMachine, WorkshopModInstanceState>()
                     .MongoDbRepository(r =>
                         {
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
                           .AddTransient<IWorkshopModOperation, WorkshopModOperation>()
                           .AddTransient<IUninstallOperation, UninstallOperation>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionPruneBuilds, ActionPruneBuilds>();
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
