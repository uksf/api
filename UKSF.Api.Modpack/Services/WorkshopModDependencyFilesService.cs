using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModDependencyFilesService
{
    void CopyPbosToDependencies(DomainWorkshopMod workshopMod, List<string> pbos, CancellationToken cancellationToken = default);
    void DeletePbosFromDependencies(List<string> pbos);
    void CopyExtensionFilesToDependencies(DomainWorkshopMod workshopMod, List<string> extensionFiles, CancellationToken cancellationToken = default);
    void DeleteExtensionFilesFromDependencies(List<string> extensionFiles);
}

public class WorkshopModDependencyFilesService(IVariablesService variablesService, IFileSystemService fileSystemService) : IWorkshopModDependencyFilesService
{
    private static readonly ParallelOptions DefaultParallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

    public void CopyPbosToDependencies(DomainWorkshopMod workshopMod, List<string> pbos, CancellationToken cancellationToken = default)
    {
        var workshopModPath = WorkshopModPaths.WorkshopMod(variablesService, workshopMod.SteamId);
        var pboPathsByName = fileSystemService.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories)
                                              .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);

        CopyToRepos(pbos, name => pboPathsByName[name], WorkshopModPaths.DependenciesAddons, cancellationToken);
    }

    public void DeletePbosFromDependencies(List<string> pbos)
    {
        DeleteFromRepos(pbos, WorkshopModPaths.DependenciesAddons);
    }

    /// <summary>
    ///     Extension DLLs are loaded by Arma from the root of a mod folder, so they sit alongside the dependencies addons
    ///     directory rather than inside it.
    /// </summary>
    public void CopyExtensionFilesToDependencies(DomainWorkshopMod workshopMod, List<string> extensionFiles, CancellationToken cancellationToken = default)
    {
        var workshopModPath = WorkshopModPaths.WorkshopMod(variablesService, workshopMod.SteamId);

        CopyToRepos(extensionFiles, name => Path.Combine(workshopModPath, name), WorkshopModPaths.Dependencies, cancellationToken);
    }

    public void DeleteExtensionFilesFromDependencies(List<string> extensionFiles)
    {
        DeleteFromRepos(extensionFiles, WorkshopModPaths.Dependencies);
    }

    private void CopyToRepos(
        List<string> fileNames,
        Func<string, string> sourceForName,
        Func<string, string> destinationFolder,
        CancellationToken cancellationToken
    )
    {
        if (fileNames.Count == 0)
        {
            return;
        }

        var copies =
            from repoPath in WorkshopModPaths.Repos(variablesService)
            from fileName in fileNames
            select (source: sourceForName(fileName), destination: Path.Combine(destinationFolder(repoPath), fileName));

        Parallel.ForEach(
            copies,
            new ParallelOptions { MaxDegreeOfParallelism = DefaultParallelOptions.MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            copy =>
            {
                fileSystemService.CreateDirectory(Path.GetDirectoryName(copy.destination)!);
                fileSystemService.CopyFile(copy.source, copy.destination, true);
            }
        );
    }

    private void DeleteFromRepos(List<string> fileNames, Func<string, string> folder)
    {
        var paths = from repoPath in WorkshopModPaths.Repos(variablesService) from fileName in fileNames select Path.Combine(folder(repoPath), fileName);

        foreach (var path in paths.Where(fileSystemService.FileExists))
        {
            fileSystemService.DeleteFile(path);
        }
    }
}
