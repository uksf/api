using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using MongoDB.Driver;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;

namespace UKSF.Api.Extensions;

public static class VerifyModeServices
{
    public static readonly IReadOnlyList<Type> ExternalHostedServices =
    [
        typeof(GameServerProcessManagerStartup),
        typeof(GameDataExportRecoveryStartup),
        typeof(DevRunRecoveryStartup),
        typeof(BackupStartupCheck),
        typeof(NpcVoiceReconciler),
        typeof(NpcMoodGenWorker),
        typeof(NpcWarmKeeper),
        typeof(NpcTraceMaintenance),
        typeof(NpcIndexes),
        typeof(MissionStatsIndexes),
        typeof(WorkshopModSagaRecoveryStartup)
    ];

    public static string DatabaseDescription(IMongoDatabase database)
    {
        var server = database.Client.Settings.Server;
        return $"verify mode: database {database.DatabaseNamespace.DatabaseName} at {server.Host}:{server.Port}";
    }

    public static string ForcedFeaturesDescription(FeatureOverrides overrides)
    {
        return $"verify mode: features forced on: {string.Join(',', overrides.ForcedOn.Order())}";
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection RemoveExternalHostedServices()
        {
            var external = services.Where(x => x.ServiceType == typeof(IHostedService) && ExternalHostedServices.Contains(x.ImplementationType)).ToList();
            external.ForEach(x => services.Remove(x));
            return services;
        }
    }
}
