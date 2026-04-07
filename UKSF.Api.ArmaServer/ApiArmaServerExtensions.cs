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

            BsonClassMap.RegisterClassMap<AceMedicalState>(cm =>
                {
                    cm.AutoMap();
                    cm.GetMemberMap(x => x.AdditionalData).SetSerializer(PlainObjectDictionaryBsonSerializer.Instance);
                }
            );

            BsonClassMap.RegisterClassMap<DomainPersistenceSession>(cm =>
                {
                    cm.AutoMap();
                    cm.GetMemberMap(x => x.Markers).SetSerializer(PlainObjectNestedListBsonSerializer.Instance);
                    cm.GetMemberMap(x => x.CustomData).SetSerializer(PlainObjectDictionaryBsonSerializer.Instance);
                }
            );

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
            return services.AddSingleton<IGameServersService, GameServersService>()
                           .AddSingleton<IMissionsService, MissionsService>()
                           .AddSingleton<IPersistenceSessionsService, PersistenceSessionsService>()
                           .AddSingleton<ILogSubscriptionService, LogSubscriptionService>()
                           .AddSingleton<IGameServerHelpers, GameServerHelpers>()
                           .AddSingleton<IRptLogService, RptLogService>()
                           .AddSingleton<ISteamCmdService, SteamCmdService>()
                           .AddTransient<IMissionStatsService, MissionStatsService>()
                           .AddTransient<IStatsEventProcessor, ShotEventProcessor>()
                           .AddTransient<IStatsEventProcessor, HitEventProcessor>()
                           .AddTransient<IStatsEventProcessor, KillEventProcessor>()
                           .AddTransient<IStatsEventProcessor, DamageEventProcessor>()
                           .AddTransient<IStatsEventProcessor, DamageReceivedEventProcessor>()
                           .AddTransient<IStatsEventProcessor, DistanceOnFootEventProcessor>()
                           .AddTransient<IStatsEventProcessor, DistanceInVehicleEventProcessor>()
                           .AddTransient<IStatsEventProcessor, FuelConsumedEventProcessor>()
                           .AddTransient<IStatsEventProcessor, ExplosivePlacedEventProcessor>()
                           .AddTransient<IStatsEventProcessor, UnconsciousEventProcessor>()
                           .AddTransient<IPerformanceService, PerformanceService>()
                           .AddScoped<IGameServerEventHandler, GameServerEventHandler>()
                           .AddSingleton<IGameServerProcessManager, GameServerProcessManager>()
                           .AddHostedService<GameServerProcessManagerStartup>();
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
