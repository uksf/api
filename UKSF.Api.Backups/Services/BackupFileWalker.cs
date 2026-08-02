using UKSF.Api.Backups.Models;

namespace UKSF.Api.Backups.Services;

public class BackupWalkFile
{
    public string SourcePath { get; set; }
    public string EntryName { get; set; }
    public string SelectionPath { get; set; }
}

public class BackupWalkResult
{
    public List<BackupWalkFile> Files { get; set; } = [];
    public List<BackupSkip> Skips { get; set; } = [];
}

public interface IBackupFileWalker
{
    BackupWalkResult Walk(IReadOnlyList<DomainBackupEntry> entries);
}

public class BackupFileWalker(IFileSystemProvider fileSystemProvider) : IBackupFileWalker
{
    public BackupWalkResult Walk(IReadOnlyList<DomainBackupEntry> entries)
    {
        var result = new BackupWalkResult();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Where(x => x.Enabled))
        {
            if (entry.EntryType == BackupEntryType.File)
            {
                WalkFile(entry, result, seen);
                continue;
            }

            WalkFolder(entry, result, seen);
        }

        return result;
    }

    private void WalkFile(DomainBackupEntry entry, BackupWalkResult result, HashSet<string> seen)
    {
        if (!fileSystemProvider.FileExists(entry.Path))
        {
            result.Skips.Add(new BackupSkip { Path = entry.Path, Reason = "File no longer exists" });
            return;
        }

        Add(entry, entry.Path, result, seen);
    }

    private void WalkFolder(DomainBackupEntry entry, BackupWalkResult result, HashSet<string> seen)
    {
        if (!fileSystemProvider.DirectoryExists(entry.Path))
        {
            result.Skips.Add(new BackupSkip { Path = entry.Path, Reason = "Folder no longer exists" });
            return;
        }

        var pending = new Queue<string>();
        pending.Enqueue(entry.Path);

        while (pending.Count > 0)
        {
            var directory = pending.Dequeue();

            foreach (var file in Read(directory, fileSystemProvider.GetFiles, result))
            {
                if (IsExcluded(entry, file))
                {
                    continue;
                }

                Add(entry, file, result, seen);
            }

            if (!entry.Recursive)
            {
                continue;
            }

            foreach (var child in Read(directory, fileSystemProvider.GetDirectories, result))
            {
                if (IsExcluded(entry, child))
                {
                    continue;
                }

                pending.Enqueue(child);
            }
        }
    }

    private static bool IsExcluded(DomainBackupEntry entry, string path)
    {
        return entry.Excludes.Any(x => BackupPaths.Contains(x, path));
    }

    private void Add(DomainBackupEntry entry, string path, BackupWalkResult result, HashSet<string> seen)
    {
        var entryName = ToEntryName(path);
        if (!seen.Add(entryName))
        {
            return;
        }

        result.Files.Add(
            new BackupWalkFile
            {
                SourcePath = BackupPaths.Normalise(path),
                EntryName = entryName,
                SelectionPath = entry.Path
            }
        );
    }

    private static IEnumerable<string> Read(string path, Func<string, IEnumerable<string>> read, BackupWalkResult result)
    {
        try
        {
            return read(path).ToList();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            result.Skips.Add(new BackupSkip { Path = BackupPaths.Normalise(path), Reason = exception.Message });
            return [];
        }
    }

    /// <summary>`C:\Server\Nginx\conf\nginx.conf` becomes `files/C/Server/Nginx/conf/nginx.conf`.</summary>
    private static string ToEntryName(string path)
    {
        var normalised = BackupPaths.Normalise(path);
        var withoutColon = normalised.Remove(1, 1);
        return "files/" + withoutColon.Replace('\\', '/').TrimStart('/');
    }
}
