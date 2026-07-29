using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModDependencyFilesService
{
    void CopyPbosToDependencies(DomainWorkshopMod workshopMod, List<string> pbos, CancellationToken cancellationToken = default);
    void DeletePbosFromDependencies(List<string> pbos);
    void CopyExtensionsToDependencies(DomainWorkshopMod workshopMod, List<string> extensions, CancellationToken cancellationToken = default);
    void DeleteExtensionsFromDependencies(List<string> extensions);
}

public class WorkshopModDependencyFilesService(IVariablesService variablesService, IFileSystemService fileSystemService) : IWorkshopModDependencyFilesService
{
    private static readonly ParallelOptions DefaultParallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

    public void CopyPbosToDependencies(DomainWorkshopMod workshopMod, List<string> pbos, CancellationToken cancellationToken = default)
    {
        var workshopModPath = WorkshopModPaths.WorkshopMod(variablesService, workshopMod.SteamId);
        var pboPathsByName = fileSystemService.EnumerateFiles(Path.Combine(workshopModPath, "addons"), "*.pbo", SearchOption.AllDirectories)
                                              .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);

        CopyToRepos(pbos, name => pboPathsByName[name], WorkshopModPaths.DependenciesAddons, cancellationToken);
    }

    public void DeletePbosFromDependencies(List<string> pbos)
    {
        DeleteFromRepos(pbos, WorkshopModPaths.DependenciesAddons);
    }

    /// <summary>Extensions go to the root of the dependencies mod folder, the only place Arma loads them from.</summary>
    public void CopyExtensionsToDependencies(DomainWorkshopMod workshopMod, List<string> extensions, CancellationToken cancellationToken = default)
    {
        var workshopModPath = WorkshopModPaths.WorkshopMod(variablesService, workshopMod.SteamId);

        CopyToRepos(extensions, name => Path.Combine(workshopModPath, name), WorkshopModPaths.Dependencies, cancellationToken);
    }

    public void DeleteExtensionsFromDependencies(List<string> extensions)
    {
        DeleteFromRepos(extensions, WorkshopModPaths.Dependencies);
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
