using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer;
using UKSF.Api.Commands;
using UKSF.Api.Discord;
using UKSF.Api.EventHandlers;
using UKSF.Api.Integrations.Instagram;
using UKSF.Api.Launcher;
using UKSF.Api.Mappers;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;
using UKSF.Api.Queries;
using UKSF.Api.ScheduledActions;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Signalr.Hubs;
using UKSF.Api.Teamspeak;

namespace UKSF.Api.Extensions;

public static class ServiceExtensions
{
    public static void AddUksf(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        services.AddSingleton(services)
                .AddContexts()
                .AddEventHandlers()
                .AddServices()
                .AddCommands()
                .AddQueries()
                .AddMappers()
                .AddActions()
                .AddMiddlewares()
                .AddSingleton<MigrationUtility>()
                .AddAutoMapper(typeof(AutoMapperUnitProfile))
                .AddComponents(configuration, currentEnvironment);
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddContext<ICommandRequestArchiveContext, CommandRequestArchiveContext>()
                       .AddCachedContext<ICommandRequestContext, CommandRequestContext>()
                       .AddCachedContext<IDischargeContext, DischargeContext>()
                       .AddCachedContext<ILoaContext, LoaContext>()
                       .AddCachedContext<IOperationOrderContext, OperationOrderContext>()
                       .AddCachedContext<IOperationReportContext, OperationReportContext>()
                       .AddCachedContext<ICommentThreadContext, CommentThreadContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<IUksfLoggerEventHandler, UksfLoggerEventHandler>()
                       .AddEventHandler<ILogDataEventHandler, LogDataEventHandler>()
                       .AddEventHandler<ICommandRequestEventHandler, CommandRequestEventHandler>()
                       .AddEventHandler<IAccountDataEventHandler, AccountDataEventHandler>()
                       .AddEventHandler<ICommentThreadEventHandler, CommentThreadEventHandler>()
                       .AddEventHandler<IDiscordEventhandler, DiscordEventhandler>()
                       .AddEventHandler<INotificationsEventHandler, NotificationsEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IDataCacheService, DataCacheService>()
                       .AddSingleton<IChainOfCommandService, ChainOfCommandService>()
                       .AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>()
                       .AddTransient<ICommandRequestService, CommandRequestService>()
                       .AddTransient<ILoaService, LoaService>()
                       .AddTransient<IOperationOrderService, OperationOrderService>()
                       .AddTransient<IOperationReportService, OperationReportService>()
                       .AddSingleton<ILoginService, LoginService>()
                       .AddSingleton<IPermissionsService, PermissionsService>()
                       .AddSingleton<IAccountService, AccountService>()
                       .AddSingleton<IAssignmentService, AssignmentService>()
                       .AddSingleton<ICommentThreadService, CommentThreadService>()
                       .AddSingleton<IRolesService, RolesService>()
                       .AddSingleton<IServiceRecordService, ServiceRecordService>();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddSingleton<IRequestPasswordResetCommand, RequestPasswordResetCommand>()
                       .AddSingleton<IResetPasswordCommand, ResetPasswordCommand>()
                       .AddSingleton<IConnectTeamspeakIdToAccountCommand, ConnectTeamspeakIdToAccountCommand>()
                       .AddSingleton<ICreateApplicationCommand, CreateApplicationCommand>()
                       .AddSingleton<ICreateCommentThreadCommand, CreateCommentThreadCommand>()
                       .AddSingleton<IQualificationsUpdateCommand, QualificationsUpdateCommand>();
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.AddSingleton<IGetCommandMembersPagedQuery, GetCommandMembersPagedQuery>()
                       .AddSingleton<IGetPagedLoasQuery, GetPagedLoasQuery>()
                       .AddSingleton<IAllNationsByAccountQuery, AllNationsByAccountQuery>()
                       .AddSingleton<IGetUnitTreeQuery, GetUnitTreeQuery>();
    }

    private static IServiceCollection AddMappers(this IServiceCollection services)
    {
        return services.AddSingleton<ICommandMemberMapper, CommandMemberMapper>()
                       .AddSingleton<ILoaMapper, LoaMapper>()
                       .AddSingleton<IUnitTreeMapper, UnitTreeMapper>();
    }

    private static IServiceCollection AddMiddlewares(this IServiceCollection services)
    {
        return services.AddSingleton<ExceptionMiddleware>().AddSingleton<CorsMiddleware>().AddSingleton<IExceptionHandler, ExceptionHandler>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionPruneLogs, ActionPruneLogs>()
                       .AddSelfCreatingScheduledAction<IActionPruneNotifications, ActionPruneNotifications>();
    }

    public static void AddUksfSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<AdminHub>($"/hub/{AdminHub.EndPoint}");
        builder.MapHub<UtilityHub>($"/hub/{UtilityHub.EndPoint}");
        builder.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.EndPoint}");
        builder.MapHub<AccountHub>($"/hub/{AccountHub.EndPoint}");
        builder.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.EndPoint}");
    }

    private static void AddComponents(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        services.AddUksfShared(configuration, currentEnvironment)
                .AddUksfAuthentication(configuration)
                .AddUksfModpack()
                .AddUksfArmaMissions()
                .AddUksfArmaServer()
                .AddUksfLauncher()
                .AddUksfIntegrationDiscord()
                .AddUksfIntegrationInstagram()
                .AddUksfIntegrationTeamspeak();
    }
}
