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
using UKSF.Api.Integrations.Instagram;
using UKSF.Api.Launcher;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;
using UKSF.Api.Personnel;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Teamspeak;

namespace UKSF.Api
{
    public static class ServiceExtensions
    {
        public static void AddUksf(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
        {
            services.AddSingleton(services).AddContexts().AddEventHandlers().AddServices().AddMiddlewares().AddSingleton<MigrationUtility>().AddComponents(configuration, currentEnvironment);
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services.AddSingleton<ILoggerEventHandler, LoggerEventHandler>();
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services;
        }

        private static IServiceCollection AddMiddlewares(this IServiceCollection services)
        {
            return services.AddSingleton<ExceptionMiddleware>().AddSingleton<CorsMiddleware>().AddSingleton<ExceptionHandler>();
        }

        private static void AddComponents(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
        {
            services.AddUksfBase(configuration, currentEnvironment)
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
}
