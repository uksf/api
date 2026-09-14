using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModRootFilesService
{
    bool SyncRootModToRepos(DomainWorkshopMod workshopMod);
    void DeleteRootModFromRepos(DomainWorkshopMod workshopMod);
    string GetRootModFolderName(DomainWorkshopMod workshopMod);
}

public class WorkshopModRootFilesService(IVariablesService variablesService, IFileSystemService fileSystemService) : IWorkshopModRootFilesService
{
    public bool SyncRootModToRepos(DomainWorkshopMod workshopMod)
    {
        var workshopModPath = WorkshopModPaths.WorkshopMod(variablesService, workshopMod.SteamId);

        var changed = false;
        foreach (var repoPath in RootModPaths(workshopMod))
        {
            changed |= SyncDirectory(workshopModPath, repoPath);
        }

        return changed;
    }

    public void DeleteRootModFromRepos(DomainWorkshopMod workshopMod)
    {
        foreach (var path in RootModPaths(workshopMod).Where(fileSystemService.DirectoryExists))
        {
            fileSystemService.DeleteDirectory(path, true);
        }
    }

    public string GetRootModFolderName(DomainWorkshopMod workshopMod)
    {
        return string.IsNullOrEmpty(workshopMod.FolderName) ? $"@{workshopMod.Name}" : workshopMod.FolderName;
    }

    private List<string> RootModPaths(DomainWorkshopMod workshopMod)
    {
        var folderName = GetRootModFolderName(workshopMod);
        return WorkshopModPaths.Repos(variablesService).Select(repoPath => Path.Combine(repoPath, folderName)).ToList();
    }

    private bool SyncDirectory(string sourceDir, string destDir)
    {
        var sourceFiles = fileSystemService.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                                           .Select(f => Path.GetRelativePath(sourceDir, f))
                                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var destFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (fileSystemService.DirectoryExists(destDir))
        {
            destFiles = fileSystemService.EnumerateFiles(destDir, "*", SearchOption.AllDirectories)
                                         .Select(f => Path.GetRelativePath(destDir, f))
                                         .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var filesChanged = false;

        foreach (var relativePath in sourceFiles)
        {
            var sourcePath = Path.Combine(sourceDir, relativePath);
            var destPath = Path.Combine(destDir, relativePath);

            var destFileExists = fileSystemService.FileExists(destPath);
            if (destFileExists && fileSystemService.AreFilesEqual(sourcePath, destPath))
            {
                continue;
            }

            var destFileDir = Path.GetDirectoryName(destPath)!;
            fileSystemService.CreateDirectory(destFileDir);
            fileSystemService.CopyFile(sourcePath, destPath, true);
            filesChanged = true;
        }

        var deletedRelativePaths = new List<string>();
        var filesToDelete = destFiles.Except(sourceFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in filesToDelete)
        {
            var destPath = Path.Combine(destDir, relativePath);
            fileSystemService.DeleteFile(destPath);
            deletedRelativePaths.Add(relativePath);
            filesChanged = true;
        }

        CleanEmptyDirectoriesAfterDeletion(destDir, deletedRelativePaths);

        return filesChanged;
    }

    private void CleanEmptyDirectoriesAfterDeletion(string destDir, List<string> deletedRelativePaths)
    {
        var parentDirs = deletedRelativePaths.Select(Path.GetDirectoryName)
                                             .Where(d => !string.IsNullOrEmpty(d))
                                             .Distinct(StringComparer.OrdinalIgnoreCase)
                                             .OrderByDescending(d => d!.Length)
                                             .ToList();

        foreach (var relativeDir in parentDirs)
        {
            var fullDir = Path.Combine(destDir, relativeDir!);
            if (fileSystemService.DirectoryExists(fullDir) && !fileSystemService.EnumerateFiles(fullDir, "*", SearchOption.AllDirectories).Any())
            {
                fileSystemService.DeleteDirectory(fullDir, true);
            }
        }
    }
}
