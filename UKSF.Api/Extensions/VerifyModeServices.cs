using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Backups.Services;

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
        typeof(NpcIndexes)
    ];

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
