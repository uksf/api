using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using UKSFWebsite.Api.Data;
using UKSFWebsite.Api.Data.Command;
using UKSFWebsite.Api.Data.Debug;
using UKSFWebsite.Api.Data.Game;
using UKSFWebsite.Api.Data.Launcher;
using UKSFWebsite.Api.Data.Message;
using UKSFWebsite.Api.Data.Operations;
using UKSFWebsite.Api.Data.Personnel;
using UKSFWebsite.Api.Data.Units;
using UKSFWebsite.Api.Data.Utility;
using UKSFWebsite.Api.Events;
using UKSFWebsite.Api.Events.Data;
using UKSFWebsite.Api.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Command;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Game;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Launcher;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Operations;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Services.Command;
using UKSFWebsite.Api.Services.Debug;
using UKSFWebsite.Api.Services.Game;
using UKSFWebsite.Api.Services.Game.Missions;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Integrations;
using UKSFWebsite.Api.Services.Integrations.Procedures;
using UKSFWebsite.Api.Services.Launcher;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Operations;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Services.Units;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api {
    public class Startup {
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment currentEnvironment;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration) {
            this.configuration = configuration;
            this.currentEnvironment = currentEnvironment;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
            LoginService.SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.GetSection("Secrets")["tokenKey"]));
            LoginService.TokenIssuer = Global.TOKEN_ISSUER;
            LoginService.TokenAudience = Global.TOKEN_AUDIENCE;
        }

        public void ConfigureServices(IServiceCollection services) {
            services.RegisterServices(configuration, currentEnvironment);
            services.AddCors(
                options => options.AddPolicy(
                    "CorsPolicy",
                    builder => { builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowCredentials().WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://integrations.uk-sf.co.uk"); }
                )
            );
            services.AddSignalR().AddNewtonsoftJsonProtocol();
            services.AddAuthentication(
                        options => {
                            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                        }
                    )
                    .AddJwtBearer(
                        options => {
                            options.TokenValidationParameters = new TokenValidationParameters {
                                RequireExpirationTime = true,
                                RequireSignedTokens = true,
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = LoginService.SecurityKey,
                                ValidateIssuer = true,
                                ValidIssuer = Global.TOKEN_ISSUER,
                                ValidateAudience = true,
                                ValidAudience = Global.TOKEN_AUDIENCE,
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.Zero
                            };
                            options.Audience = Global.TOKEN_AUDIENCE;
                            options.ClaimsIssuer = Global.TOKEN_ISSUER;
                            options.SaveToken = true;
                            options.Events = new JwtBearerEvents {
                                OnMessageReceived = context => {
                                    StringValues accessToken = context.Request.Query["access_token"];

                                    if (!string.IsNullOrEmpty(accessToken) && context.Request.Path.StartsWithSegments("/hub")) {
                                        context.Token = accessToken;
                                    }

                                    return Task.CompletedTask;
                                }
                            };
                        }
                    );

            ExceptionHandler.Instance = new ExceptionHandler();
            services.AddControllers();
            services.AddMvc(options => { options.Filters.Add(ExceptionHandler.Instance); }).AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env, ILoggerFactory loggerFactory) {
            app.UseHsts();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseCorsMiddleware();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(
                endpoints => {
                    endpoints.MapControllers();
                    endpoints.MapHub<AccountHub>($"/hub/{AccountHub.END_POINT}");
                    endpoints.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
                    endpoints.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.END_POINT}");
                    endpoints.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.END_POINT}");
                    endpoints.MapHub<NotificationHub>($"/hub/{NotificationHub.END_POINT}");
                    endpoints.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.END_POINT}");
                    endpoints.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
                    endpoints.MapHub<ServersHub>($"/hub/{ServersHub.END_POINT}");
                    endpoints.MapHub<LauncherHub>($"/hub/{LauncherHub.END_POINT}");
                }
            );

            Global.ServiceProvider = app.ApplicationServices;
            Services.ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            // Initialise exception handler
            ExceptionHandler.Instance.Initialise(Global.ServiceProvider.GetService<ISessionService>(), Global.ServiceProvider.GetService<IDisplayNameService>());

            // Execute any DB migration
            Global.ServiceProvider.GetService<MigrationUtility>().Migrate();

            // Warm caches
            WarmDataServices();
            
            // Add event handlers
            Global.ServiceProvider.GetService<EventHandlerInitialiser>().InitEventHandlers();

            // Connect discord bot
            Global.ServiceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start pipe connection
            Global.ServiceProvider.GetService<IPipeManager>().Start();

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().Load();
        }

        private static void WarmDataServices() {
            DataCacheService dataCacheService = Global.ServiceProvider.GetService<DataCacheService>();
            List<Type> servicesTypes = AppDomain.CurrentDomain.GetAssemblies()
                                                .SelectMany(x => x.GetTypes())
                                                .Where(x => !x.IsAbstract && !x.IsInterface && x.BaseType != null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(CachedDataService<>))
                                                .Select(x => x.GetInterfaces().Reverse().FirstOrDefault(y => !y.IsGenericType))
                                                .ToList();
            foreach (object service in servicesTypes.Select(type => Global.ServiceProvider.GetService(type))) {
                dataCacheService.AddDataService((dynamic) service);
            }
            dataCacheService.InvalidateDataCaches();
        }
    }

    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment) {
            RegisterEventServices(services);
            RegisterDataServices(services, currentEnvironment);
            RegisterDataBackedServices(services, currentEnvironment);

            // Instance Objects
            services.AddTransient<IAssignmentService, AssignmentService>();
            services.AddTransient<IAttendanceService, AttendanceService>();
            services.AddTransient<IChainOfCommandService, ChainOfCommandService>();
            services.AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>();
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddTransient<ILauncherService, LauncherService>();
            services.AddTransient<ILoginService, LoginService>();
            services.AddTransient<IMissionPatchingService, MissionPatchingService>();
            services.AddTransient<IRecruitmentService, RecruitmentService>();
            services.AddTransient<IServerService, ServerService>();
            services.AddTransient<IServiceRecordService, ServiceRecordService>();
            services.AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
            services.AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>();
            services.AddTransient<MissionPatchDataService>();
            services.AddTransient<MissionService>();

            // Global Singletons
            services.AddSingleton(configuration);
            services.AddSingleton(currentEnvironment);
            services.AddSingleton(_ => MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddSingleton<DataCacheService>();
            services.AddSingleton<MigrationUtility>();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<ITeamspeakService, TeamspeakService>();

            // TeamSpeak procedures
            services.AddSingleton<CheckClientServerGroup>();
            services.AddSingleton<Pong>();
            services.AddSingleton<SendClientsUpdate>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<IDiscordService, FakeDiscordService>();
                services.AddSingleton<IPipeManager, FakePipeManager>();
            } else {
                services.AddSingleton<IDiscordService, DiscordService>();
                services.AddSingleton<IPipeManager, PipeManager>();
            }
        }

        private static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddTransient<IEventBus, DataEventBus>();
            
            // Event Handlers
            services.AddSingleton<EventHandlerInitialiser>();
            services.AddSingleton<IAccountEventHandler, AccountEventHandler>();
            services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();
            services.AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>();
            services.AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();
        }

        private static void RegisterDataServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddTransient<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddTransient<ISchedulerDataService, SchedulerDataService>();

            // Cached
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<ICommandRequestDataService, CommandRequestDataService>();
            services.AddSingleton<ICommandRequestArchiveDataService, CommandRequestArchiveDataService>();
            services.AddSingleton<ICommentThreadDataService, CommentThreadDataService>();
            services.AddSingleton<IDischargeDataService, DischargeDataService>();
            services.AddSingleton<IGameServersDataService, GameServersDataService>();
            services.AddSingleton<ILauncherFileDataService, LauncherFileDataService>();
            services.AddSingleton<ILoaDataService, LoaDataService>();
            services.AddSingleton<IOperationOrderDataService, OperationOrderDataService>();
            services.AddSingleton<IOperationReportDataService, OperationReportDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<IRolesDataService, RolesDataService>();
            services.AddSingleton<IUnitsDataService, UnitsDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<INotificationsDataService, FakeNotificationsDataService>();
            } else {
                services.AddSingleton<INotificationsDataService, NotificationsDataService>();
            }
        }

        private static void RegisterDataBackedServices(this IServiceCollection services, IHostEnvironment currentEnvironment) {
            // Non-Cached
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            // Cached
            services.AddSingleton<IAccountService, AccountService>();
            services.AddTransient<ICommandRequestService, CommandRequestService>();
            services.AddTransient<ICommentThreadService, CommentThreadService>();
            services.AddTransient<IDischargeService, DischargeService>();
            services.AddTransient<IGameServersService, GameServersService>();
            services.AddTransient<ILauncherFileService, LauncherFileService>();
            services.AddTransient<ILoaService, LoaService>();
            services.AddTransient<IOperationOrderService, OperationOrderService>();
            services.AddTransient<IOperationReportService, OperationReportService>();
            services.AddTransient<IRanksService, RanksService>();
            services.AddTransient<IRolesService, RolesService>();
            services.AddTransient<IUnitsService, UnitsService>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddTransient<INotificationsService, FakeNotificationsService>();
            } else {
                services.AddTransient<INotificationsService, NotificationsService>();
            }
        }
    }

    public class CorsMiddleware {
        private readonly RequestDelegate next;

        public CorsMiddleware(RequestDelegate next) => this.next = next;

        public Task Invoke(HttpContext httpContext) {
            if (httpContext.Request.Path.Value.Contains("hub")) {
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = httpContext.Request.Headers["Origin"];
                httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            }

            return next(httpContext);
        }
    }

    public static class CorsMiddlewareExtensions {
        public static void UseCorsMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<CorsMiddleware>();
    }
}

// Request Singletons
// services.AddScoped<>();
