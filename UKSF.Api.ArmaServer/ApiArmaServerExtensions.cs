using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.ArmaServer;

public static class ApiArmaServerExtensions
{
    public static IServiceCollection AddUksfArmaServer(this IServiceCollection services)
    {
        return services.AddContexts().AddEventHandlers().AddServices().AddCommands().AddQueries().AddActions();
    }

    private static IServiceCollection AddContexts(this IServiceCollection services)
    {
        return services.AddCachedContext<IGameServersContext, GameServersContext>();
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services.AddSingleton<IGameServersService, GameServersService>()
                       .AddSingleton<IGameServerHelpers, GameServerHelpers>()
                       .AddSingleton<ISteamCmdService, SteamCmdService>();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        return services.AddSingleton<IUpdateServerInfrastructureCommand, UpdateServerInfrastructureCommand>();
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        return services.AddSingleton<IGetLatestServerInfrastructureQuery, GetLatestServerInfrastructureQuery>()
                       .AddSingleton<IGetCurrentServerInfrastructureQuery, GetCurrentServerInfrastructureQuery>()
                       .AddSingleton<IGetInstalledServerInfrastructureQuery, GetInstalledServerInfrastructureQuery>();
    }

    private static IServiceCollection AddActions(this IServiceCollection services)
    {
        return services.AddSelfCreatingScheduledAction<IActionCheckForServerUpdate, ActionCheckForServerUpdate>();
    }

    public static void AddUksfArmaServerSignalr(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<ServersHub>($"/hub/{ServersHub.EndPoint}");
    }
}
