using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.ScheduledActions;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Backups;

public static class ApiBackupsExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUksfBackups()
        {
            return services.AddContexts().AddServices().AddActions().AddHostedService<BackupStartupCheck>();
        }

        private IServiceCollection AddContexts()
        {
            return services.AddContext<IBackupEntriesContext, BackupEntriesContext>().AddContext<IBackupRunsContext, BackupRunsContext>();
        }

        private IServiceCollection AddServices()
        {
            return services.AddSingleton<IFileSystemProvider, FileSystemProvider>()
                           .AddSingleton<IFileTreeService, FileTreeService>()
                           .AddSingleton<IBackupSelectionService, BackupSelectionService>()
                           .AddSingleton<IBackupFileWalker, BackupFileWalker>()
                           .AddSingleton<IBackupArchiveService, BackupArchiveService>()
                           .AddSingleton<IBackupEncryptionService, BackupEncryptionService>()
                           .AddSingleton<IProcessRunner, ProcessRunner>()
                           .AddSingleton<IMongoDumpService, MongoDumpService>()
                           .AddSingleton<IGoogleDriveClient, GoogleDriveClient>()
                           .AddSingleton<IBackupRetentionService, BackupRetentionService>()
                           .AddSingleton<IBackupAlertService, BackupAlertService>()
                           .AddSingleton<IBackupWatchdog, BackupWatchdog>()
                           .AddSingleton<IBackupRunner, BackupRunner>();
        }

        private IServiceCollection AddActions()
        {
            return services.AddSelfCreatingScheduledAction<IActionRunBackup, ActionRunBackup>()
                           .AddSelfCreatingScheduledAction<IActionCheckBackup, ActionCheckBackup>();
        }
    }
}
