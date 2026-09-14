using UKSF.Api.Backups.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public interface IBackupRetentionService
{
    int Retention { get; }
    void PruneLocal(string directory);
    Task PruneRemote(CancellationToken cancellationToken = default);
    Task EnsureRemoteSpace(long requiredBytes, CancellationToken cancellationToken = default);
}

/// <summary>
///     Keeps the newest N archives on disk and in Drive. The old setup filled Drive because nothing ever deleted an
///     old version; here every upload is preceded by a space check and followed by an explicit prune.
/// </summary>
public class BackupRetentionService(
    IVariablesService variablesService,
    IGoogleDriveClient googleDriveClient,
    IFileSystemProvider fileSystemProvider,
    IUksfLogger logger
) : IBackupRetentionService
{
    private const int DefaultRetention = 2;

    public int Retention => Math.Max(1, variablesService.GetVariable("BACKUP_RETENTION").AsIntWithDefault(DefaultRetention));

    public void PruneLocal(string directory)
    {
        if (!fileSystemProvider.DirectoryExists(directory))
        {
            return;
        }

        var archives = fileSystemProvider.GetFiles(directory).Where(BackupArchiveNaming.IsArchive).OrderByDescending(BackupArchiveNaming.SortKey).ToList();

        foreach (var stale in archives.Skip(Retention))
        {
            fileSystemProvider.DeleteFile(stale);
            logger.LogInfo($"Backup pruned local archive {stale}");
        }
    }

    public async Task PruneRemote(CancellationToken cancellationToken = default)
    {
        var archives = await RemoteArchives(cancellationToken);

        foreach (var stale in archives.Skip(Retention))
        {
            await googleDriveClient.Delete(stale.Id, cancellationToken);
            logger.LogInfo($"Backup pruned Drive archive {stale.Name}");
        }
    }

    public async Task EnsureRemoteSpace(long requiredBytes, CancellationToken cancellationToken = default)
    {
        var quota = await googleDriveClient.GetQuota(cancellationToken);
        if (quota.FreeBytes >= requiredBytes)
        {
            return;
        }

        var archives = await RemoteArchives(cancellationToken);
        var free = quota.FreeBytes;

        // Never delete the newest archive to make room - that is the copy being relied on until this run finishes.
        foreach (var stale in archives.Skip(1).Reverse())
        {
            await googleDriveClient.Delete(stale.Id, cancellationToken);
            free += stale.Bytes;
            logger.LogInfo($"Backup deleted Drive archive {stale.Name} to make room ({stale.Bytes} bytes)");

            if (free >= requiredBytes)
            {
                return;
            }
        }

        throw new UksfException($"Drive has {free} bytes free, {requiredBytes} needed, and every prunable archive is already gone", 500);
    }

    private async Task<List<BackupCloudFile>> RemoteArchives(CancellationToken cancellationToken)
    {
        var files = await googleDriveClient.List(cancellationToken);
        return files.Where(x => BackupArchiveNaming.IsArchive(x.Name)).OrderByDescending(x => BackupArchiveNaming.SortKey(x.Name)).ToList();
    }
}
