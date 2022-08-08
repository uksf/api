using UKSF.Api.Admin;
using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer;
using UKSF.Api.Auth;
using UKSF.Api.Base;
using UKSF.Api.Base.Configuration;
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
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Teamspeak;

namespace UKSF.Api;

public static class ServiceExtensions
{
    public static void AddUksf(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment)
    {
        services.AddSingleton(services)
                .AddConfiguration(configuration)
                .AddContexts()
                .AddEventHandlers()
                .AddServices()
                .AddMiddlewares()
                .AddSingleton<MigrationUtility>()
                .AddComponents(configuration, currentEnvironment);
    }

    private static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        return services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services.AddEventHandler<IUksfLoggerEventHandler, UksfLoggerEventHandler>();
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddMiddlewares(this IServiceCollection services)
    {
        return services.AddSingleton<ExceptionMiddleware>().AddSingleton<CorsMiddleware>().AddSingleton<IExceptionHandler, ExceptionHandler>();
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
