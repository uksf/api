using MassTransit;
using MongoDB.Driver;
using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.EventHandlers;
using UKSF.Api.Integrations.Discord;
using UKSF.Api.Integrations.Instagram;
using UKSF.Api.Integrations.Teamspeak;
using UKSF.Api.Launcher;
using UKSF.Api.Mappers;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;
using UKSF.Api.ArmaServer.Consumers;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using UKSF.Api.Queries;
using UKSF.Api.ScheduledActions;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.Extensions;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksf(IConfiguration configuration, IHostEnvironment currentEnvironment)
        {
            return services.AddSingleton(services)
                           .AddContexts()
                           .AddEventHandlers()
                           .AddServices()
                           .AddCommands()
                           .AddQueries()
                           .AddMappers()
                           .AddActions()
                           .AddMiddlewares()
                           .AddSingleton<MigrationUtility>()
                           .AddComponents(configuration, currentEnvironment);
        }

        private IServiceCollection AddContexts()
        {
            return services.AddContext<ICommandRequestArchiveContext, CommandRequestArchiveContext>()
                           .AddCachedContext<ICommandRequestContext, CommandRequestContext>()
                           .AddCachedContext<IDischargeContext, DischargeContext>()
                           .AddCachedContext<ILoaContext, LoaContext>()
                           .AddCachedContext<IOperationOrderContext, OperationOrderContext>()
                           .AddCachedContext<IOperationReportContext, OperationReportContext>()
                           .AddCachedContext<ICommentThreadContext, CommentThreadContext>();
        }

        private IServiceCollection AddEventHandlers()
        {
            return services.AddEventHandler<IUksfLoggerEventHandler, UksfLoggerEventHandler>()
                           .AddEventHandler<ILogDataEventHandler, LogDataEventHandler>()
                           .AddEventHandler<ICommandRequestEventHandler, CommandRequestEventHandler>()
                           .AddEventHandler<IAccountDataEventHandler, AccountDataEventHandler>()
                           .AddEventHandler<ICommentThreadEventHandler, CommentThreadEventHandler>()
                           .AddEventHandler<IDiscordEventHandler, DiscordEventHandler>()
                           .AddEventHandler<INotificationsEventHandler, NotificationsEventHandler>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<IDataCacheService, DataCacheService>()
                           .AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>()
                           .AddTransient<ICommandRequestService, CommandRequestService>()
                           .AddTransient<ILoaService, LoaService>()
                           .AddTransient<IOperationOrderService, OperationOrderService>()
                           .AddTransient<IOperationReportService, OperationReportService>()
                           .AddTransient<ILoginService, LoginService>()
                           .AddTransient<IPermissionsService, PermissionsService>()
                           .AddTransient<ICommentThreadService, CommentThreadService>()
                           .AddScoped<IDocumentFolderService, DocumentFolderService>()
                           .AddScoped<IDocumentService, DocumentService>()
                           .AddScoped<IDocumentPermissionsService, DocumentPermissionsService>();
        }

        private IServiceCollection AddCommands()
        {
            return services.AddTransient<IRequestPasswordResetCommand, RequestPasswordResetCommand>()
                           .AddTransient<IResetPasswordCommand, ResetPasswordCommand>()
                           .AddTransient<IConnectTeamspeakIdToAccountCommand, ConnectTeamspeakIdToAccountCommand>()
                           .AddTransient<ICreateApplicationCommand, CreateApplicationCommand>()
                           .AddTransient<ICreateCommentThreadCommand, CreateCommentThreadCommand>()
                           .AddTransient<IQualificationsUpdateCommand, QualificationsUpdateCommand>()
                           .AddTransient<IUpdateUnitCommandHandler, UpdateUnitCommandHandler>();
        }

        private IServiceCollection AddQueries()
        {
            return services.AddTransient<IGetCommandMembersPagedQuery, GetCommandMembersPagedQuery>()
                           .AddTransient<IGetPagedLoasQuery, GetPagedLoasQuery>()
                           .AddTransient<IAllNationsByAccountQuery, AllNationsByAccountQuery>()
                           .AddTransient<IGetUnitTreeQuery, GetUnitTreeQuery>()
                           .AddTransient<IGetCompletedApplicationsPagedQueryHandler, GetCompletedApplicationsPagedQueryHandler>();
        }

        private IServiceCollection AddMappers()
        {
            return services.AddTransient<ICommandMemberMapper, CommandMemberMapper>()
                           .AddTransient<ILoaMapper, LoaMapper>()
                           .AddTransient<IUnitTreeMapper, UnitTreeMapper>()
                           .AddTransient<ICompletedApplicationMapper, CompletedApplicationMapper>();
        }

        private IServiceCollection AddMiddlewares()
        {
            return services.AddSingleton<ExceptionMiddleware>().AddSingleton<CorsMiddleware>().AddSingleton<IExceptionHandler, ExceptionHandler>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionPruneLogs, ActionPruneLogs>()
                           .AddSelfCreatingScheduledAction<IActionPruneNotifications, ActionPruneNotifications>();
        }

        private IServiceCollection AddComponents(IConfiguration configuration, IHostEnvironment currentEnvironment)
        {
            return services.AddUksfShared(configuration, currentEnvironment)
                           .AddUksfAuthentication(configuration)
                           .AddUksfMassTransit(configuration)
                           .AddUksfModpack()
                           .AddUksfArmaMissions()
                           .AddUksfArmaServer()
                           .AddUksfLauncher()
                           .AddUksfIntegrationDiscord()
                           .AddUksfIntegrationInstagram()
                           .AddUksfIntegrationTeamspeak();
        }

        private IServiceCollection AddUksfMassTransit(IConfiguration configuration)
        {
            var appSettings = new AppSettings();
            configuration.GetSection(nameof(AppSettings)).Bind(appSettings);

            services.AddMassTransit(x =>
                {
                    x.AddConsumer<ProcessMissionStatsBatchConsumer>();
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

            return services;
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfSignalr()
        {
            builder.MapHub<AdminHub>($"/hub/{AdminHub.EndPoint}");
            builder.MapHub<UtilityHub>($"/hub/{UtilityHub.EndPoint}");
            builder.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.EndPoint}");
            builder.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.EndPoint}");
        }
    }
}
