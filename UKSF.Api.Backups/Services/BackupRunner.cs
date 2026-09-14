using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public interface IBackupRunner
{
    Task<DomainBackupRun> Run(CancellationToken cancellationToken = default);

    /// <summary>Records the run and returns at once, so a manual trigger does not sit on an HTTP request for an hour.</summary>
    Task<DomainBackupRun> Start();
}

public class BackupRunner(
    IBackupRunsContext backupRunsContext,
    IBackupSelectionService backupSelectionService,
    IMongoDumpService mongoDumpService,
    IBackupArchiveService backupArchiveService,
    IBackupEncryptionService backupEncryptionService,
    IBackupRetentionService backupRetentionService,
    IGoogleDriveClient googleDriveClient,
    IFileSystemProvider fileSystemProvider,
    IBackupAlertService backupAlertService,
    IVariablesService variablesService,
    IClock clock,
    IUksfLogger logger
) : IBackupRunner
{
    private const string DefaultBackupPath = @"E:\Backups";

    public async Task<DomainBackupRun> Start()
    {
        var run = await CreateRun();

        _ = Task.Run(async () =>
            {
                try
                {
                    await Execute(run, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning($"Backup {run.Id} ended: {exception.Message}");
                }
            }
        );

        return run;
    }

    public async Task<DomainBackupRun> Run(CancellationToken cancellationToken = default)
    {
        var run = await CreateRun();
        return await Execute(run, cancellationToken);
    }

    private async Task<DomainBackupRun> CreateRun()
    {
        var run = new DomainBackupRun { Started = clock.UtcNow(), State = BackupRunState.Running };
        await backupRunsContext.Add(run);
        return run;
    }

    private async Task<DomainBackupRun> Execute(DomainBackupRun run, CancellationToken cancellationToken)
    {
        var backupPath = variablesService.GetVariable("BACKUP_PATH").AsStringWithDefault(DefaultBackupPath);
        var stagingPath = Path.Combine(backupPath, $"staging-{run.Id}");

        try
        {
            await Build(run, backupPath, stagingPath, cancellationToken);

            run.State = BackupRunState.Success;
            run.Finished = clock.UtcNow();
            await backupRunsContext.Replace(run);

            logger.LogInfo($"Backup {run.ArchiveName} complete - {run.FileCount} files, {run.ArchiveBytes} bytes");
            return run;
        }
        catch (Exception exception)
        {
            run.State = BackupRunState.Failed;
            run.Finished = clock.UtcNow();
            run.Error = exception.Message;
            await backupRunsContext.Replace(run);

            await backupAlertService.Alert($"run failed: {exception.Message}");
            throw;
        }
        finally
        {
            Cleanup(stagingPath);
        }
    }

    private async Task Build(DomainBackupRun run, string backupPath, string stagingPath, CancellationToken cancellationToken)
    {
        fileSystemProvider.CreateDirectory(backupPath);
        fileSystemProvider.CreateDirectory(stagingPath);

        var mongoPath = Path.Combine(stagingPath, "mongo");
        var dumps = await mongoDumpService.Dump(mongoPath, cancellationToken);
        run.Databases = dumps.Select(x => x.Database).ToList();

        var entries = backupSelectionService.GetEntries().Where(x => x.Enabled).ToList();
        if (entries.Count == 0 && dumps.Count == 0)
        {
            throw new UksfException("Backup has nothing to archive - no entries are selected", 500);
        }

        var zipPath = Path.Combine(stagingPath, "archive.zip");
        var manifest = await WriteZip(entries, dumps, zipPath, cancellationToken);

        run.FileCount = manifest.FileCount;
        run.RawBytes = manifest.RawBytes;
        run.Skips = manifest.Skips;
        run.ArchiveName = BackupArchiveNaming.ForTime(run.Started);
        run.LocalPath = Path.Combine(backupPath, run.ArchiveName);

        await Encrypt(zipPath, run.LocalPath, cancellationToken);
        run.ArchiveBytes = fileSystemProvider.GetFileSize(run.LocalPath);

        backupRetentionService.PruneLocal(backupPath);

        await Upload(run, cancellationToken);
    }

    private async Task<BackupManifest> WriteZip(List<DomainBackupEntry> entries, List<MongoDumpFile> dumps, string zipPath, CancellationToken cancellationToken)
    {
        var extras = dumps.Select(x => new BackupWalkFile
                              {
                                  SourcePath = x.Path,
                                  EntryName = $"mongo/{Path.GetFileName(x.Path)}",
                                  SelectionPath = "mongo"
                              }
                          )
                          .ToList();

        await using var zipStream = fileSystemProvider.Create(zipPath);
        return await backupArchiveService.WriteArchive(entries, zipStream, extras, cancellationToken);
    }

    private async Task Encrypt(string zipPath, string archivePath, CancellationToken cancellationToken)
    {
        var publicKey = variablesService.GetVariable("BACKUP_PUBLIC_KEY").AsStringWithDefault(null);
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            throw new UksfException("Backup cannot run - variable 'BACKUP_PUBLIC_KEY' is missing", 500);
        }

        await using var input = fileSystemProvider.OpenRead(zipPath);
        await using var output = fileSystemProvider.Create(archivePath);
        await backupEncryptionService.Encrypt(input, output, publicKey, cancellationToken);
    }

    private async Task Upload(DomainBackupRun run, CancellationToken cancellationToken)
    {
        if (!variablesService.GetVariable("BACKUP_DRIVE_ENABLED").AsBoolWithDefault(true))
        {
            logger.LogWarning($"Backup {run.ArchiveName} was not uploaded - BACKUP_DRIVE_ENABLED is off");
            return;
        }

        await backupRetentionService.EnsureRemoteSpace(run.ArchiveBytes, cancellationToken);
        var uploaded = await googleDriveClient.Upload(run.LocalPath, run.ArchiveName, cancellationToken);
        run.DriveFileId = uploaded.Id;

        await backupRetentionService.PruneRemote(cancellationToken);
    }

    private void Cleanup(string stagingPath)
    {
        try
        {
            fileSystemProvider.DeleteDirectory(stagingPath);
        }
        catch (Exception exception)
        {
            logger.LogWarning($"Backup could not clean staging {stagingPath}: {exception.Message}");
        }
    }
}
