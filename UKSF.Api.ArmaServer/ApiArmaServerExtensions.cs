using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer;

public static class ApiArmaServerExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfArmaServer()
        {
            return services.AddContexts().AddEventHandlers().AddServices().AddCommands().AddQueries().AddActions();
        }

        private IServiceCollection AddContexts()
        {
            return services.AddCachedContext<IGameServersContext, GameServersContext>();
        }

        private IServiceCollection AddEventHandlers()
        {
            return services;
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<IGameServersService, GameServersService>()
                           .AddSingleton<IGameServerHelpers, GameServerHelpers>()
                           .AddSingleton<ISteamCmdService, SteamCmdService>();
        }

        private IServiceCollection AddCommands()
        {
            return services.AddTransient<IUpdateServerInfrastructureCommand, UpdateServerInfrastructureCommand>();
        }

        private IServiceCollection AddQueries()
        {
            return services.AddTransient<IGetLatestServerInfrastructureQuery, GetLatestServerInfrastructureQuery>()
                           .AddTransient<IGetCurrentServerInfrastructureQuery, GetCurrentServerInfrastructureQuery>()
                           .AddTransient<IGetInstalledServerInfrastructureQuery, GetInstalledServerInfrastructureQuery>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionCheckForServerUpdate, ActionCheckForServerUpdate>()
                           .AddSelfCreatingScheduledAction<IActionCleanupRunningServers, ActionCleanupRunningServers>();
        }
    }

    extension(IEndpointRouteBuilder builder)
    {
        public void AddUksfArmaServerSignalr()
        {
            builder.MapHub<ServersHub>($"/hub/{ServersHub.EndPoint}");
        }
    }
}
