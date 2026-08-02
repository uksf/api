using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Exceptions;

namespace UKSF.Api.Backups.Services;

public interface IFileTreeService
{
    IEnumerable<BackupTreeNode> GetRoots();
    IEnumerable<BackupTreeNode> GetChildren(string path);
}

public class FileTreeService(IFileSystemProvider fileSystemProvider) : IFileTreeService
{
    public IEnumerable<BackupTreeNode> GetRoots()
    {
        return fileSystemProvider.GetDrives()
                                 .Select(BackupPaths.Normalise)
                                 .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                                 .Select(x => new BackupTreeNode
                                     {
                                         Name = x,
                                         Path = x,
                                         IsDirectory = true,
                                         HasChildren = true
                                     }
                                 )
                                 .ToList();
    }

    public IEnumerable<BackupTreeNode> GetChildren(string path)
    {
        var normalised = BackupPaths.Normalise(path);
        if (!fileSystemProvider.DirectoryExists(normalised))
        {
            throw new UksfException($"Folder not found: {normalised}", 404);
        }

        var directories = Read(normalised, fileSystemProvider.GetDirectories).Select(x => ToNode(x, true));
        var files = Read(normalised, fileSystemProvider.GetFiles).Select(x => ToNode(x, false));

        return directories.Concat(files).ToList();
    }

    // A broken junction throws on enumeration - C:\Server\Arma\Environments\Release holds several - so an
    // unreadable child must not take out the whole listing.
    private static IEnumerable<string> Read(string path, Func<string, IEnumerable<string>> read)
    {
        try
        {
            return read(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private BackupTreeNode ToNode(string path, bool isDirectory)
    {
        var normalised = BackupPaths.Normalise(path);
        return new BackupTreeNode
        {
            Name = normalised.Split('\\').Last(),
            Path = normalised,
            IsDirectory = isDirectory,
            HasChildren = isDirectory && HasChildren(normalised)
        };
    }

    private bool HasChildren(string path)
    {
        try
        {
            return fileSystemProvider.GetDirectories(path).Any() || fileSystemProvider.GetFiles(path).Any();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
