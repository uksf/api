using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using UKSFWebsite.Api.Data;
using UKSFWebsite.Api.Data.Admin;
using UKSFWebsite.Api.Data.Command;
using UKSFWebsite.Api.Data.Fake;
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
using UKSFWebsite.Api.Events.SignalrServer;
using UKSFWebsite.Api.Interfaces.Command;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Events.Handlers;
using UKSFWebsite.Api.Interfaces.Game;
using UKSFWebsite.Api.Interfaces.Integrations;
using UKSFWebsite.Api.Interfaces.Integrations.Teamspeak;
using UKSFWebsite.Api.Interfaces.Launcher;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Interfaces.Operations;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Admin;
using UKSFWebsite.Api.Services.Command;
using UKSFWebsite.Api.Services.Fake;
using UKSFWebsite.Api.Services.Game;
using UKSFWebsite.Api.Services.Game.Missions;
using UKSFWebsite.Api.Services.Integrations;
using UKSFWebsite.Api.Services.Integrations.Teamspeak;
using UKSFWebsite.Api.Services.Launcher;
using UKSFWebsite.Api.Services.Message;
using UKSFWebsite.Api.Services.Operations;
using UKSFWebsite.Api.Services.Personnel;
using UKSFWebsite.Api.Services.Units;
using UKSFWebsite.Api.Services.Utility;
using UKSFWebsite.Api.Signalr.Hubs.Command;
using UKSFWebsite.Api.Signalr.Hubs.Game;
using UKSFWebsite.Api.Signalr.Hubs.Integrations;
using UKSFWebsite.Api.Signalr.Hubs.Message;
using UKSFWebsite.Api.Signalr.Hubs.Personnel;
using UKSFWebsite.Api.Signalr.Hubs.Utility;

namespace UKSFWebsite.Api {
    public class Startup {
        private readonly IConfiguration configuration;
        private readonly IHostEnvironment currentEnvironment;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration) {
            this.configuration = configuration;
            this.currentEnvironment = currentEnvironment;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
            Console.Out.WriteLine(configuration.GetChildren().Select(x => $"{x.Key}, {x.Value}").Aggregate((x,y) => $"{x}, {y}"));
            LoginService.SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.GetSection("Secrets")["tokenKey"]));
            LoginService.TokenIssuer = Global.TOKEN_ISSUER;
            LoginService.TokenAudience = Global.TOKEN_AUDIENCE;
        }

        public void ConfigureServices(IServiceCollection services) {
            services.RegisterServices(configuration, currentEnvironment);
            services.AddCors(options => options.AddPolicy("CorsPolicy", builder => { builder.AllowAnyMethod().AllowAnyHeader().WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://integrations.uk-sf.co.uk").AllowCredentials(); }));
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

        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app) {
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseCorsMiddleware();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();
            app.UseForwardedHeaders(new ForwardedHeadersOptions {ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto});
            app.UseEndpoints(
                endpoints => {
                    endpoints.MapControllers().RequireCors("CorsPolicy");
                    endpoints.MapHub<AccountHub>($"/hub/{AccountHub.END_POINT}");
                    endpoints.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
                    endpoints.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.END_POINT}");
                    endpoints.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.END_POINT}");
                    endpoints.MapHub<NotificationHub>($"/hub/{NotificationHub.END_POINT}");
                    endpoints.MapHub<TeamspeakHub>($"/hub/{TeamspeakHub.END_POINT}").RequireHost("localhost");
                    endpoints.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.END_POINT}");
                    endpoints.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
                    endpoints.MapHub<ServersHub>($"/hub/{ServersHub.END_POINT}");
                    endpoints.MapHub<LauncherHub>($"/hub/{LauncherHub.END_POINT}");
                }
            );

            Global.ServiceProvider = app.ApplicationServices;
            ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            // Initialise exception handler
            ExceptionHandler.Instance.Initialise(Global.ServiceProvider.GetService<ISessionService>(), Global.ServiceProvider.GetService<IDisplayNameService>());

            // Execute any DB migration
            Global.ServiceProvider.GetService<MigrationUtility>().Migrate();

            // Warm cached data services
            WarmDataServices();

            // Add event handlers
            Global.ServiceProvider.GetService<EventHandlerInitialiser>().InitEventHandlers();

            // Start teamspeak manager
//            Global.ServiceProvider.GetService<ITeamspeakManager>().Start();

            // Connect discord bot
            Global.ServiceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().Load();
        }

        private static void WarmDataServices() {
            DataCacheService dataCacheService = Global.ServiceProvider.GetService<DataCacheService>();
            List<Type> servicesTypes = AppDomain.CurrentDomain.GetAssemblies()
                                                .SelectMany(x => x.GetTypes())
                                                .Where(x => !x.IsAbstract && !x.IsInterface && x.BaseType != null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(CachedDataService<,>))
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

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<ITeamspeakManager, FakeTeamspeakManager>();
                services.AddSingleton<IDiscordService, FakeDiscordService>();
            } else {
                services.AddSingleton<ITeamspeakManager, TeamspeakManager>();
                services.AddSingleton<IDiscordService, DiscordService>();
            }
        }

        private static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddSingleton<IDataEventBus<IAccountDataService>, DataEventBus<IAccountDataService>>();
            services.AddSingleton<IDataEventBus<ICommandRequestDataService>, DataEventBus<ICommandRequestDataService>>();
            services.AddSingleton<IDataEventBus<ICommandRequestArchiveDataService>, DataEventBus<ICommandRequestArchiveDataService>>();
            services.AddSingleton<IDataEventBus<ICommentThreadDataService>, DataEventBus<ICommentThreadDataService>>();
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<IDischargeDataService>, DataEventBus<IDischargeDataService>>();
            services.AddSingleton<IDataEventBus<IGameServersDataService>, DataEventBus<IGameServersDataService>>();
            services.AddSingleton<IDataEventBus<ILauncherFileDataService>, DataEventBus<ILauncherFileDataService>>();
            services.AddSingleton<IDataEventBus<ILoaDataService>, DataEventBus<ILoaDataService>>();
            services.AddSingleton<IDataEventBus<INotificationsDataService>, DataEventBus<INotificationsDataService>>();
            services.AddSingleton<IDataEventBus<IOperationOrderDataService>, DataEventBus<IOperationOrderDataService>>();
            services.AddSingleton<IDataEventBus<IOperationReportDataService>, DataEventBus<IOperationReportDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IRanksDataService>, DataEventBus<IRanksDataService>>();
            services.AddSingleton<IDataEventBus<IRolesDataService>, DataEventBus<IRolesDataService>>();
            services.AddSingleton<IDataEventBus<IUnitsDataService>, DataEventBus<IUnitsDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();
            services.AddSingleton<ISignalrEventBus, SignalrEventBus>();

            // Event Handlers
            services.AddSingleton<EventHandlerInitialiser>();
            services.AddSingleton<IAccountEventHandler, AccountEventHandler>();
            services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();
            services.AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>();
            services.AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();
            services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>();
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

    // ReSharper disable once ClassNeverInstantiated.Global
    public class CorsMiddleware {
        private readonly RequestDelegate next;

        public CorsMiddleware(RequestDelegate next) => this.next = next;

        // ReSharper disable once UnusedMember.Global
        public Task Invoke(HttpContext httpContext) {
            if (httpContext.Request.Path.Value.Contains("hub")) {
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = httpContext.Request.Headers["Origin"];
                httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            }

            return next(httpContext);
        }
    }

    public static class CorsMiddlewareExtensions {
        // ReSharper disable once UnusedMethodReturnValue.Global
        public static IApplicationBuilder UseCorsMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<CorsMiddleware>();
    }

    public static class HttpRequestExtensions {
        public static bool IsLocal(this HttpRequest req) {
            ConnectionInfo connection = req.HttpContext.Connection;
            if (connection.RemoteIpAddress != null) {
                if (connection.LocalIpAddress != null) {
                    return connection.RemoteIpAddress.Equals(connection.LocalIpAddress);
                }

                return IPAddress.IsLoopback(connection.RemoteIpAddress);
            }

            // for in memory TestServer or when dealing with default connection info
            if (connection.RemoteIpAddress == null && connection.LocalIpAddress == null) {
                return true;
            }

            return false;
        }
    }
}

// Request Singletons
// services.AddScoped<>();
