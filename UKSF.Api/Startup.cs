using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.OpenApi.Models;
using UKSF.Api.AppStart;
using UKSF.Api.Data;
using UKSF.Api.Events;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Common;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;
using UKSF.Api.Signalr.Hubs.Command;
using UKSF.Api.Signalr.Hubs.Game;
using UKSF.Api.Signalr.Hubs.Integrations;
using UKSF.Api.Signalr.Hubs.Message;
using UKSF.Api.Signalr.Hubs.Personnel;
using UKSF.Api.Signalr.Hubs.Utility;

namespace UKSF.Api {
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
                    builder => {
                        builder.AllowAnyMethod()
                               .AllowAnyHeader()
                               .WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://integrations.uk-sf.co.uk")
                               .AllowCredentials();
                    }
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
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new OpenApiInfo { Title = "UKSF API", Version = "v1" }); });
            services.AddMvc(options => { options.Filters.Add(ExceptionHandler.Instance); }).AddNewtonsoftJson();
        }

        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, IHostApplicationLifetime hostApplicationLifetime) {
            hostApplicationLifetime.ApplicationStopping.Register(OnShutdown);
            app.UseStaticFiles();
            app.UseCookiePolicy(new CookiePolicyOptions {MinimumSameSitePolicy = SameSiteMode.Lax});
            app.UseSwagger();
            app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF API v1"); });
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseCorsMiddleware();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
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
            Global.ServiceProvider.GetService<ITeamspeakManagerService>().Start();

            // Connect discord bot
            Global.ServiceProvider.GetService<IDiscordService>().ConnectDiscord();

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().Load();
        }

        private static void WarmDataServices() {
            // TODO: Redo this trash
            DataCacheService dataCacheService = Global.ServiceProvider.GetService<DataCacheService>();
            List<Type> servicesTypes = AppDomain.CurrentDomain.GetAssemblies()
                                                .SelectMany(x => x.GetTypes())
                                                .Where(
                                                    x => !x.IsAbstract &&
                                                         !x.IsInterface &&
                                                         x.BaseType != null &&
                                                         x.BaseType.IsGenericType &&
                                                         x.BaseType.GetGenericTypeDefinition() == typeof(CachedDataService<,>)
                                                )
                                                .Select(x => x.GetInterfaces().Reverse().FirstOrDefault(y => !y.IsGenericType))
                                                .ToList();
            foreach (object service in servicesTypes.Select(type => Global.ServiceProvider.GetService(type))) {
                dataCacheService.AddDataService((dynamic) service);
            }

            dataCacheService.InvalidateDataCaches();
        }

        private static void OnShutdown() {
            // Stop teamspeak
            Global.ServiceProvider.GetService<ITeamspeakManagerService>().Stop();
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
}
