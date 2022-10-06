using System.Text.Json;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Configuration;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Converters;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Mappers;
using UKSF.Api.Shared.Queries;
using UKSF.Api.Shared.ScheduledActions;
using UKSF.Api.Shared.Services;
using UKSF.Api.Shared.Signalr.Hubs;

namespace UKSF.Api.Shared;

public static class ApiSharedExtensions
{
    public static IServiceCollection AddUksfShared(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        var appSettings = new AppSettings();
        configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
        services.AddConfiguration(configuration)
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
                .AddSingleton<IClock, Clock>();
        services.AddSignalR()
                .AddJsonProtocol(
                    options =>
                    {
                        options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                        options.PayloadSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                        options.PayloadSerializerOptions.Converters.Add(new InferredTypeConverter());
                    }
                );
        return services;
    }

    private static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        return services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddContext<ILogContext, LogContext>()
                       .AddContext<IAuditLogContext, AuditLogContext>()
                       .AddContext<IErrorLogContext, ErrorLogContext>()
                       .AddContext<ILauncherLogContext, LauncherLogContext>()
                       .AddContext<IDiscordLogContext, DiscordLogContext>()
                       .AddContext<ISchedulerContext, SchedulerContext>()
                       .AddContext<IMigrationContext, MigrationContext>()
                       .AddContext<IConfirmationCodeContext, ConfirmationCodeContext>()
                       .AddCachedContext<IVariablesContext, VariablesContext>()
                       .AddSingleton<ISmtpClientContext, SmtpClientContext>()
                       .AddSingleton<IFileContext, FileContext>()
                       .AddCachedContext<IAccountContext, AccountContext>()
                       .AddCachedContext<IRanksContext, RanksContext>()
                       .AddCachedContext<IRolesContext, RolesContext>()
                       .AddCachedContext<IUnitsContext, UnitsContext>()
                       .AddCachedContext<INotificationsContext, NotificationsContext>();
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
                       .AddSingleton<IDisplayNameService, DisplayNameService>()
                       .AddSingleton<IRanksService, RanksService>()
                       .AddSingleton<IUnitsService, UnitsService>()
                       .AddSingleton<IConfirmationCodeService, ConfirmationCodeService>()
                       .AddSingleton<INotificationsService, NotificationsService>()
                       .AddSingleton<IRecruitmentService, RecruitmentService>()
                       .AddSingleton<IObjectIdConversionService, ObjectIdConversionService>();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddSingleton<ISendTemplatedEmailCommand, SendTemplatedEmailCommand>().AddSingleton<ISendBasicEmailCommand, SendBasicEmailCommand>();
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.AddSingleton<IGetEmailTemplateQuery, GetEmailTemplateQuery>();
    }

    private static IServiceCollection AddMappers(this IServiceCollection services)
    {
        return services.AddSingleton<IAccountMapper, AccountMapper>();
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
    }

    private static IMongoDatabase GetDatabase(string connectionString)
    {
        ConventionPack conventionPack =
            new() { new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true), new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("DefaultConventions", conventionPack, _ => true);
        var database = MongoUrl.Create(connectionString).DatabaseName;
        return new MongoClient(connectionString).GetDatabase(database);
    }
}
