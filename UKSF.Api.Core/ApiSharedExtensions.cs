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
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfShared(IConfiguration configuration, IHostEnvironment currentEnvironment)
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

        private IServiceCollection AddConfiguration(IConfiguration configuration, AppSettings appSettings)
        {
            return services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings))).AddSingleton(appSettings);
        }

        private IServiceCollection AddContexts()
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

        private IServiceCollection AddEventHandlers()
        {
            return services;
        }

        private IServiceCollection AddServices()
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
                           .AddSingleton<IGitService, GitService>()
                           .AddSingleton<IFileSystemService, FileSystemService>();
        }

        private IServiceCollection AddCommands()
        {
            return services.AddTransient<ISendTemplatedEmailCommand, SendTemplatedEmailCommand>()
                           .AddTransient<ISendBasicEmailCommand, SendBasicEmailCommand>()
                           .AddTransient<IUpdateApplicationCommand, UpdateApplicationCommand>()
                           .AddTransient<IUpdateAccountTrainingCommandHandler, UpdateAccountTrainingCommandHandler>();
        }

        private IServiceCollection AddQueries()
        {
            return services.AddTransient<IGetEmailTemplateQuery, GetEmailTemplateQuery>().AddTransient<IBuildUrlQuery, BuildUrlQuery>();
        }

        private IServiceCollection AddMappers()
        {
            return services.AddTransient<IAccountMapper, AccountMapper>().AddTransient<IUnitMapper, UnitMapper>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddScheduledAction<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>();
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfSharedSignalr()
        {
            builder.MapHub<AllHub>($"/hub/{AllHub.EndPoint}");
            builder.MapHub<AccountGroupedHub>($"/hub/{AccountGroupedHub.EndPoint}");
            builder.MapHub<NotificationHub>($"/hub/{NotificationHub.EndPoint}");
            builder.MapHub<AccountHub>($"/hub/{AccountHub.EndPoint}");
        }
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
