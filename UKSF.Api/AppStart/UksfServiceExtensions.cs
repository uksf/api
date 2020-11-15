using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin;
using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer;
using UKSF.Api.Auth;
using UKSF.Api.Base;
using UKSF.Api.Command;
using UKSF.Api.Discord;
using UKSF.Api.EventHandlers;
using UKSF.Api.Integration.Instagram;
using UKSF.Api.Launcher;
using UKSF.Api.Modpack;
using UKSF.Api.Personnel;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Teamspeak;

namespace UKSF.Api.AppStart {
    public static class ServiceExtensions {
        public static IServiceCollection AddUksf(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment) =>
            services.AddContexts()
                    .AddEventBuses()
                    .AddEventHandlers()
                    .AddServices()
                    .AddSingleton(configuration)
                    .AddSingleton(currentEnvironment)
                    .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                    .AddSingleton<ExceptionHandler>()
                    .AddSingleton<MigrationUtility>()
                    .AddComponents(configuration);

        private static IServiceCollection AddContexts(this IServiceCollection services) => services;

        private static IServiceCollection AddEventBuses(this IServiceCollection services) => services;

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<ILoggerEventHandler, LoggerEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) => services;

        private static IServiceCollection AddComponents(this IServiceCollection services, IConfiguration configuration) =>
            services.AddUksfBase(configuration)
                    .AddUksfShared()
                    .AddUksfAuth(configuration)
                    .AddUksfAdmin()
                    .AddUksfCommand()
                    .AddUksfModpack()
                    .AddUksfPersonnel()
                    .AddUksfArmaMissions()
                    .AddUksfArmaServer()
                    .AddUksfLauncher()
                    .AddUksfIntegrationDiscord()
                    .AddUksfIntegrationInstagram()
                    .AddUksfIntegrationTeamspeak();
    }
}
