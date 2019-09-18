using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Cors.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Debug;
using UKSFWebsite.Api.Services.Hubs;
using UKSFWebsite.Api.Services.Launcher;
using UKSFWebsite.Api.Services.Logging;
using UKSFWebsite.Api.Services.Missions;
using UKSFWebsite.Api.Services.Teamspeak;
using UKSFWebsite.Api.Services.Teamspeak.Procedures;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api {
    public class Startup {
        private readonly IConfiguration configuration;
        private readonly IHostingEnvironment currentEnvironment;

        public Startup(IHostingEnvironment currentEnvironment, IConfiguration configuration) {
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
            services.BuildServiceProvider();
            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new Info {Title = "UKSF API", Version = "v1"}); });
            services.AddCors(
                options => options.AddPolicy("CorsPolicy", builder => { builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowCredentials().WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://steam.uk-sf.co.uk"); })
            );
            services.AddSignalR();
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
            services.AddMvc(
                options => {
                    options.Filters.Add(ExceptionHandler.Instance);
                    options.Filters.Add(new CorsAuthorizationFilterFactory("CorsPolicy"));
                }
            );
        }

        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {
            app.UseCors("CorsPolicy");
            app.UseCorsMiddleware();
            app.UseAuthentication();
            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs"); });
            app.UseStaticFiles();
            app.UseSignalR(
                route => {
                    route.MapHub<AccountHub>($"/hub/{AccountHub.END_POINT}");
                    route.MapHub<AdminHub>($"/hub/{AdminHub.END_POINT}");
                    route.MapHub<CommandRequestsHub>($"/hub/{CommandRequestsHub.END_POINT}");
                    route.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.END_POINT}");
                    route.MapHub<NotificationHub>($"/hub/{NotificationHub.END_POINT}");
                    route.MapHub<TeamspeakClientsHub>($"/hub/{TeamspeakClientsHub.END_POINT}");
                    route.MapHub<UtilityHub>($"/hub/{UtilityHub.END_POINT}");
                    route.MapHub<ServersHub>($"/hub/{ServersHub.END_POINT}");
                    route.MapHub<LauncherHub>($"/hub/{LauncherHub.END_POINT}");
                }
            );
            app.UseMvc();
            app.UseHsts();
            app.UseHttpsRedirection();

            Global.ServiceProvider = app.ApplicationServices;
            ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            // Initialise exception handler
            ExceptionHandler.Instance.Initialise(Global.ServiceProvider.GetService<ISessionService>(), Global.ServiceProvider.GetService<IDisplayNameService>());

            // Execute any DB migration
            Global.ServiceProvider.GetService<MigrationUtility>().Migrate();

            // Warm caches
            WarmDataServices();

            // Connect discord bot
            Global.ServiceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start pipe connection
            Global.ServiceProvider.GetService<IPipeManager>().Start();

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().Load();
        }

        private static void WarmDataServices() {
            CacheService cacheService = Global.ServiceProvider.GetService<CacheService>();
            List<Type> servicesTypes = AppDomain.CurrentDomain.GetAssemblies()
                                                .SelectMany(x => x.GetTypes())
                                                .Where(x => !x.IsAbstract && !x.IsInterface && x.BaseType != null && x.BaseType.IsGenericType && x.BaseType.GetGenericTypeDefinition() == typeof(CachedDataService<>))
                                                .Select(x => x.GetInterfaces().FirstOrDefault(y => !y.IsGenericType))
                                                .ToList();
            foreach (Type type in servicesTypes) {
                dynamic service = Global.ServiceProvider.GetService(type);
                cacheService.AddService(service);
                service.Get();
            }
        }
    }

    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration, IHostingEnvironment currentEnvironment) {
            // Instance Objects
            services.AddTransient<IAssignmentService, AssignmentService>();
            services.AddTransient<IAttendanceService, AttendanceService>();
            services.AddTransient<IChainOfCommandService, ChainOfCommandService>();
            services.AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>();
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddTransient<ILauncherService, LauncherService>();
            services.AddTransient<ILoginService, LoginService>();
            services.AddTransient<IMissionPatchingService, MissionPatchingService>();
            services.AddTransient<IRecruitmentService, RecruitmentService>();
            services.AddTransient<IServerService, ServerService>();
            services.AddTransient<IServiceRecordService, ServiceRecordService>();
            services.AddTransient<ISchedulerService, SchedulerService>();
            services.AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
            services.AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>();
            services.AddTransient<MissionPatchDataService>();
            services.AddTransient<MissionService>();

            // Request Singletons
            // services.AddScoped<>();

            // Global Singletons
            services.AddSingleton(configuration);
            services.AddSingleton(currentEnvironment);
            services.AddSingleton(_ => MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddSingleton<CacheService>();
            services.AddSingleton<MigrationUtility>();
            services.AddSingleton<IAccountService, AccountService>();
            services.AddSingleton<ICommandRequestService, CommandRequestService>();
            services.AddSingleton<ICommentThreadService, CommentThreadService>();
            services.AddSingleton<IDischargeService, DischargeService>();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<IGameServersService, GameServersService>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<ILauncherFileService, LauncherFileService>();
            services.AddSingleton<ILoaService, LoaService>();
            services.AddSingleton<ILogging, Logging>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IOperationOrderService, OperationOrderService>();
            services.AddSingleton<IOperationReportService, OperationReportService>();
            services.AddSingleton<IRanksService, RanksService>();
            services.AddSingleton<IRolesService, RolesService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<ITeamspeakService, TeamspeakService>();
            services.AddSingleton<IUnitsService, UnitsService>();
            services.AddSingleton<IVariablesService, VariablesService>();

            // TeamSpeak procedures
            services.AddSingleton<CheckClientServerGroup>();
            services.AddSingleton<Pong>();
            services.AddSingleton<SendClientsUpdate>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<IDiscordService, FakeDiscordService>();
                services.AddSingleton<INotificationsService, FakeNotificationsService>();
                services.AddSingleton<IPipeManager, FakePipeManager>();
            } else {
                services.AddSingleton<IDiscordService, DiscordService>();
                services.AddSingleton<INotificationsService, NotificationsService>();
                services.AddSingleton<IPipeManager, PipeManager>();
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
        public static IApplicationBuilder UseCorsMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<CorsMiddleware>();
    }
}
