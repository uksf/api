using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using UKSF.Api.ArmaServer.Commands;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Queries;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Services.StatsEventProcessors;
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
            BsonSerializer.RegisterSerializer(new JsonElementBsonSerializer());

            return services.AddContexts().AddServices().AddCommands().AddQueries().AddActions().AddHostedService<MissionStatsIndexes>();
        }

        private IServiceCollection AddContexts()
        {
            return services.AddCachedContext<IGameServersContext, GameServersContext>()
                           .AddContext<IMissionSessionsContext, MissionSessionsContext>()
                           .AddContext<IMissionStatsBatchesContext, MissionStatsBatchesContext>()
                           .AddContext<IPlayerMissionStatsContext, PlayerMissionStatsContext>()
                           .AddContext<IMissionStatsContext, MissionStatsContext>()
                           .AddContext<IPersistenceSessionsContext, PersistenceSessionsContext>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddScoped<IGameServersService, GameServersService>()
                           .AddScoped<IMissionsService, MissionsService>()
                           .AddSingleton<IPersistenceSessionsService, PersistenceSessionsService>()
                           .AddSingleton<ILogSubscriptionService, LogSubscriptionService>()
                           .AddSingleton<IGameServerHelpers, GameServerHelpers>()
                           .AddSingleton<IRptLogService, RptLogService>()
                           .AddSingleton<ISteamCmdService, SteamCmdService>()
                           .AddTransient<IMissionStatsService, MissionStatsService>()
                           .AddTransient<IStatsEventProcessor, ShotEventProcessor>()
                           .AddTransient<IStatsEventProcessor, HitEventProcessor>()
                           .AddSingleton<IGameServerProcessMonitor, GameServerProcessMonitor>()
                           .AddHostedService<GameServerProcessMonitorStartup>();
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
