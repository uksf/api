using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UKSF.Api.Backups.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Backups.Services;

public interface IBackupArchiveService
{
    Task<BackupManifest> WriteArchive(
        IReadOnlyList<DomainBackupEntry> entries,
        Stream output,
        IReadOnlyList<BackupWalkFile> extraFiles = null,
        CancellationToken cancellationToken = default
    );
}

public class BackupArchiveService(IFileSystemProvider fileSystemProvider, IBackupFileWalker backupFileWalker, IClock clock, IUksfLogger logger)
    : IBackupArchiveService
{
    private const string ManifestName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<BackupManifest> WriteArchive(
        IReadOnlyList<DomainBackupEntry> entries,
        Stream output,
        IReadOnlyList<BackupWalkFile> extraFiles = null,
        CancellationToken cancellationToken = default
    )
    {
        var walk = backupFileWalker.Walk(entries);
        var files = walk.Files.Concat(extraFiles ?? []).ToList();
        var manifest = CreateManifest(entries, walk);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var written = await WriteFile(archive, file, manifest, cancellationToken);
            if (!written)
            {
                continue;
            }

            manifest.FileCount++;
        }

        await WriteManifest(archive, manifest, cancellationToken);
        return manifest;
    }

    private BackupManifest CreateManifest(IReadOnlyList<DomainBackupEntry> entries, BackupWalkResult walk)
    {
        return new BackupManifest
        {
            CreatedUtc = clock.UtcNow(),
            MachineName = Environment.MachineName,
            Skips = walk.Skips,
            Entries = entries.Where(x => x.Enabled)
                             .Select(x => new BackupManifestEntry
                                 {
                                     Path = x.Path,
                                     EntryType = x.EntryType,
                                     Recursive = x.Recursive,
                                     Excludes = x.Excludes
                                 }
                             )
                             .ToList()
        };
    }

    private async Task<bool> WriteFile(ZipArchive archive, BackupWalkFile file, BackupManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            var size = fileSystemProvider.GetFileSize(file.SourcePath);

            var archiveEntry = archive.CreateEntry(file.EntryName, CompressionLevel.Optimal);
            archiveEntry.LastWriteTime = fileSystemProvider.GetLastWriteTimeUtc(file.SourcePath);

            await using (var source = fileSystemProvider.OpenRead(file.SourcePath))
                await using (var target = archiveEntry.Open())
                {
                    await source.CopyToAsync(target, cancellationToken);
                }

            manifest.RawBytes += size;

            var manifestEntry = manifest.Entries.FirstOrDefault(x => x.Path == file.SelectionPath);
            if (manifestEntry is null)
            {
                manifestEntry = new BackupManifestEntry { Path = file.SelectionPath, EntryType = BackupEntryType.File };
                manifest.Entries.Add(manifestEntry);
            }

            manifestEntry.FileCount++;
            manifestEntry.RawBytes += size;

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            logger.LogWarning($"Backup skipped {file.SourcePath}: {exception.Message}");
            manifest.Skips.Add(new BackupSkip { Path = file.SourcePath, Reason = exception.Message });
            return false;
        }
    }

    private static async Task WriteManifest(ZipArchive archive, BackupManifest manifest, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
        await using var target = entry.Open();
        await target.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, JsonOptions)), cancellationToken);
    }
}
