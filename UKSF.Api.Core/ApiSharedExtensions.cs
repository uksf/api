using System.Text.Json;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Converters;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Queries;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Core.Signalr.Hubs;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.Core;

public static class ApiSharedExtensions
{
    public static IServiceCollection AddUksfShared(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        var appSettings = new AppSettings();
        configuration.GetSection(nameof(AppSettings)).Bind(appSettings);

        services.AddConfiguration(configuration, appSettings)
                .AddContexts()
                .AddEventHandlers()
                .AddServices()
                .AddCommands()
                .AddQueries()
                .AddMappers()
                .AddActions()
                .AddSingleton(configuration)
                .AddSingleton(currentEnvironment)
                .AddTransient<IHttpContextService, HttpContextService>()
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                .AddSingleton(GetDatabase(appSettings.ConnectionStrings.Database))
                .AddSingleton<IEventBus, EventBus>()
                .AddTransient<IMongoCollectionFactory, MongoCollectionFactory>()
                .AddSingleton<IUksfLogger, UksfLogger>()
                .AddSingleton<IClock, Clock>()
                .AddSingleton<IProcessCommandFactory, ProcessCommandFactory>();

        services.AddSignalR()
                .AddJsonProtocol(options =>
                    {
                        options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                        options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                        options.PayloadSerializerOptions.Converters.Add(new InferredTypeConverter());
                    }
                );
        return services;
    }

    private static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration, AppSettings appSettings)
    {
        return services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings))).AddSingleton(appSettings);
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddSingleton<ISmtpClientContext, SmtpClientContext>()
                       .AddSingleton<IFileContext, FileContext>()
                       .AddContext<ILogContext, LogContext>()
                       .AddContext<IAuditLogContext, AuditLogContext>()
                       .AddContext<IConfirmationCodeContext, ConfirmationCodeContext>()
                       .AddContext<IDiscordLogContext, DiscordLogContext>()
                       .AddContext<IErrorLogContext, ErrorLogContext>()
                       .AddContext<ILauncherLogContext, LauncherLogContext>()
                       .AddContext<IMigrationContext, MigrationContext>()
                       .AddContext<ISchedulerContext, SchedulerContext>()
                       .AddCachedContext<IAccountContext, AccountContext>()
                       .AddCachedContext<IArtilleryContext, ArtilleryContext>()
                       .AddCachedContext<IDocumentFolderMetadataContext, DocumentFolderMetadataContext>()
                       .AddCachedContext<INotificationsContext, NotificationsContext>()
                       .AddCachedContext<IRanksContext, RanksContext>()
                       .AddCachedContext<IRolesContext, RolesContext>()
                       .AddCachedContext<ITrainingsContext, TrainingsContext>()
                       .AddCachedContext<IUnitsContext, UnitsContext>()
                       .AddCachedContext<IVariablesContext, VariablesContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IScheduledActionFactory, ScheduledActionFactory>()
                       .AddTransient<ISchedulerService, SchedulerService>()
                       .AddSingleton<IVariablesService, VariablesService>()
                       .AddSingleton<IStaticVariablesService, StaticVariablesService>()
                       .AddSingleton<IDisplayNameService, DisplayNameService>()
                       .AddSingleton<IRanksService, RanksService>()
                       .AddSingleton<IChainOfCommandService, ChainOfCommandService>()
                       .AddSingleton<IUnitsService, UnitsService>()
                       .AddSingleton<IConfirmationCodeService, ConfirmationCodeService>()
                       .AddSingleton<INotificationsService, NotificationsService>()
                       .AddSingleton<IRecruitmentService, RecruitmentService>()
                       .AddSingleton<IObjectIdConversionService, ObjectIdConversionService>()
                       .AddSingleton<IAccountService, AccountService>()
                       .AddSingleton<IAssignmentService, AssignmentService>()
                       .AddSingleton<IServiceRecordService, ServiceRecordService>()
                       .AddSingleton<IGitService, GitService>();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddSingleton<ISendTemplatedEmailCommand, SendTemplatedEmailCommand>()
                       .AddSingleton<ISendBasicEmailCommand, SendBasicEmailCommand>()
                       .AddSingleton<IUpdateApplicationCommand, UpdateApplicationCommand>()
                       .AddSingleton<IUpdateAccountTrainingCommandHandler, UpdateAccountTrainingCommandHandler>();
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.AddSingleton<IGetEmailTemplateQuery, GetEmailTemplateQuery>().AddSingleton<IBuildUrlQuery, BuildUrlQuery>();
    }

    private static IServiceCollection AddMappers(this IServiceCollection services)
    {
        return services.AddSingleton<IAccountMapper, AccountMapper>().AddSingleton<IUnitMapper, UnitMapper>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddScheduledAction<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>();
    }

    public static void AddUksfSharedSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<AllHub>($"/hub/{AllHub.EndPoint}");
        builder.MapHub<AccountGroupedHub>($"/hub/{AccountGroupedHub.EndPoint}");
        builder.MapHub<NotificationHub>($"/hub/{NotificationHub.EndPoint}");
        builder.MapHub<AccountHub>($"/hub/{AccountHub.EndPoint}");
    }

    private static IMongoDatabase GetDatabase(string connectionString)
    {
        ConventionPack conventionPack =
        [
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true),
            new CamelCaseElementNameConvention()
        ];
        ConventionRegistry.Register("DefaultConventions", conventionPack, _ => true);

        var database = MongoUrl.Create(connectionString).DatabaseName;
        return new MongoClient(connectionString).GetDatabase(database);
    }
}
