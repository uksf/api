using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModsProcessingService
{
    Task DownloadWithRetries(string workshopModId, int maxRetries = 2, CancellationToken cancellationToken = default);
    string GetWorkshopModPath(string workshopModId);
    List<string> GetModFiles(string workshopModPath);
    Task CopyPbosToDependencies(DomainWorkshopMod domainWorkshopMod, List<string> pbos, CancellationToken cancellationToken = default);
    void DeletePbosFromDependencies(List<string> pbos);
    Task CopyRootModToRepos(DomainWorkshopMod workshopMod, CancellationToken cancellationToken = default);
    bool SyncRootModToRepos(DomainWorkshopMod workshopMod);
    void DeleteRootModFromRepos(DomainWorkshopMod workshopMod);
    string GetRootModFolderName(DomainWorkshopMod workshopMod);
    void CleanupWorkshopModFiles(string workshopModPath);
    Task QueueDevBuild();
    Task UpdateModStatus(DomainWorkshopMod workshopMod, WorkshopModStatus status, string message);
    Task SetAvailablePbos(DomainWorkshopMod workshopMod, List<string> pbos);
}

public class WorkshopModsProcessingService(
    IWorkshopModsContext workshopModsContext,
    IVariablesService variablesService,
    ISteamCmdService steamCmdService,
    IModpackService modpackService,
    IFileSystemService fileSystemService,
    IUksfLogger logger
) : IWorkshopModsProcessingService
{
    public async Task DownloadWithRetries(string workshopModId, int maxRetries = 2, CancellationToken cancellationToken = default)
    {
        var retryDelay = TimeSpan.FromSeconds(5);

        var firstRoundException = await TryDownloadWithRetries(workshopModId, maxRetries, retryDelay, cancellationToken);
        if (firstRoundException == null)
        {
            return;
        }

        logger.LogWarning($"All {maxRetries} download attempts failed for {workshopModId}. Clearing workshop cache and retrying");
        ClearWorkshopCache();

        var secondRoundException = await TryDownloadWithRetries(workshopModId, 1, retryDelay, cancellationToken);
        if (secondRoundException == null)
        {
            return;
        }

        throw new Exception($"Unable to download after clearing cache: {secondRoundException.Message}", secondRoundException);
    }

    private async Task<Exception> TryDownloadWithRetries(string workshopModId, int maxRetries, TimeSpan retryDelay, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await steamCmdService.DownloadWorkshopMod(workshopModId);
                return null;
            }
            catch (Exception exception)
            {
                lastException = exception;
                if (attempt < maxRetries)
                {
                    logger.LogWarning($"Download attempt {attempt}/{maxRetries} failed for {workshopModId}: {exception.Message}");
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }

        return lastException;
    }

    private void ClearWorkshopCache()
    {
        var steamPath = variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        var workshopPath = Path.Combine(steamPath, "steamapps", "workshop");

        var manifestFile = Path.Combine(workshopPath, "appworkshop_107410.acf");
        if (fileSystemService.FileExists(manifestFile))
        {
            logger.LogWarning($"Deleting Steam workshop manifest: {manifestFile}");
            fileSystemService.DeleteFile(manifestFile);
        }

        var downloadsPath = Path.Combine(workshopPath, "downloads");
        if (fileSystemService.DirectoryExists(downloadsPath))
        {
            logger.LogWarning($"Clearing Steam workshop downloads: {downloadsPath}");
            fileSystemService.DeleteDirectory(downloadsPath, true);
        }
    }

    public string GetWorkshopModPath(string workshopModId)
    {
        var steamPath = variablesService.GetVariable("SERVER_PATH_STEAM").AsString();
        return Path.Combine(steamPath, "steamapps", "workshop", "content", "107410", workshopModId);
    }

    public List<string> GetModFiles(string workshopModPath)
    {
        var pboFiles = Directory.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();

        if (pboFiles.Count == 0)
        {
            throw new InvalidOperationException($"No PBO files found in {workshopModPath}");
        }

        var duplicates = pboFiles.GroupBy(f => f!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicates.Count != 0)
        {
            throw new InvalidOperationException($"Duplicate PBO names found: {string.Join(", ", duplicates)}. Manual investigation required.");
        }

        return pboFiles;
    }

    public async Task CopyPbosToDependencies(DomainWorkshopMod domainWorkshopMod, List<string> pbos, CancellationToken cancellationToken = default)
    {
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", "@uksf_dependencies", "addons");
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", "@uksf_dependencies", "addons");
        var workshopModPath = GetWorkshopModPath(domainWorkshopMod.SteamId);

        var allPboFiles = Directory.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories)
                                   .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);

        var copyOperations = pbos.SelectMany(pboName =>
            {
                var sourcePath = allPboFiles[pboName];
                return new[] { (sourcePath, dest: Path.Combine(devPath, pboName)), (sourcePath, dest: Path.Combine(rcPath, pboName)) };
            }
        );

        await Parallel.ForEachAsync(
            copyOperations,
            new ParallelOptions { MaxDegreeOfParallelism = DefaultParallelOptions.MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (op, token) =>
            {
                await using var sourceStream = File.OpenRead(op.sourcePath);
                await using var destinationStream = File.Create(op.dest);
                await sourceStream.CopyToAsync(destinationStream, token);
            }
        );
    }

    public void DeletePbosFromDependencies(List<string> pbos)
    {
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", "@uksf_dependencies", "addons");
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", "@uksf_dependencies", "addons");

        foreach (var pbo in pbos)
        {
            var devFile = Path.Combine(devPath, pbo);
            var rcFile = Path.Combine(rcPath, pbo);

            if (fileSystemService.FileExists(devFile))
            {
                fileSystemService.DeleteFile(devFile);
            }

            if (fileSystemService.FileExists(rcFile))
            {
                fileSystemService.DeleteFile(rcFile);
            }
        }
    }

    public async Task CopyRootModToRepos(DomainWorkshopMod workshopMod, CancellationToken cancellationToken = default)
    {
        var folderName = GetRootModFolderName(workshopMod);
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", folderName);
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", folderName);
        var workshopModPath = GetWorkshopModPath(workshopMod.SteamId);

        await Task.WhenAll(CopyDirectoryAsync(workshopModPath, devPath, cancellationToken), CopyDirectoryAsync(workshopModPath, rcPath, cancellationToken));
    }

    public bool SyncRootModToRepos(DomainWorkshopMod workshopMod)
    {
        var folderName = GetRootModFolderName(workshopMod);
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", folderName);
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", folderName);
        var workshopModPath = GetWorkshopModPath(workshopMod.SteamId);

        var devChanged = SyncDirectory(workshopModPath, devPath);
        var rcChanged = SyncDirectory(workshopModPath, rcPath);

        return devChanged || rcChanged;
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

    public void DeleteRootModFromRepos(DomainWorkshopMod workshopMod)
    {
        var folderName = GetRootModFolderName(workshopMod);
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", folderName);
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", folderName);

        if (fileSystemService.DirectoryExists(devPath))
        {
            fileSystemService.DeleteDirectory(devPath, true);
        }

        if (fileSystemService.DirectoryExists(rcPath))
        {
            fileSystemService.DeleteDirectory(rcPath, true);
        }
    }

    public string GetRootModFolderName(DomainWorkshopMod workshopMod)
    {
        return string.IsNullOrEmpty(workshopMod.FolderName) ? $"@{workshopMod.Name}" : workshopMod.FolderName;
    }

    public void CleanupWorkshopModFiles(string workshopModPath)
    {
        if (fileSystemService.DirectoryExists(workshopModPath))
        {
            fileSystemService.DeleteDirectory(workshopModPath, true);
        }
    }

    public async Task QueueDevBuild()
    {
        var skipBuild = variablesService.GetVariable("WORKSHOP_SKIP_DEV_BUILD")?.AsBool() ?? false;
        if (skipBuild)
        {
            logger.LogInfo("Skipping dev build queue (WORKSHOP_SKIP_DEV_BUILD is enabled)");
            return;
        }

        try
        {
            var runningBuilds = modpackService.GetDevBuilds().Where(b => b.Running).ToList();
            foreach (var build in runningBuilds)
            {
                await modpackService.CancelBuild(build);
            }

            await modpackService.NewBuild(new NewBuild { Reference = "main" });
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to trigger dev build after workshop mod change", exception);
        }
    }

    public async Task UpdateModStatus(DomainWorkshopMod workshopMod, WorkshopModStatus status, string message)
    {
        workshopMod.Status = status;
        if (status == WorkshopModStatus.Error)
        {
            workshopMod.StatusMessage = "An error occurred";
            workshopMod.ErrorMessage = message;
        }
        else
        {
            workshopMod.StatusMessage = message;
            workshopMod.ErrorMessage = null;
        }

        await workshopModsContext.Replace(workshopMod);
    }

    public async Task SetAvailablePbos(DomainWorkshopMod workshopMod, List<string> pbos)
    {
        workshopMod.Pbos = pbos;
        await workshopModsContext.Replace(workshopMod);
    }

    private static readonly ParallelOptions DefaultParallelOptions = new() { MaxDegreeOfParallelism = Environment.ProcessorCount };

    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken cancellationToken = default)
    {
        var sourceFiles = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories);

        await Parallel.ForEachAsync(
            sourceFiles,
            new ParallelOptions { MaxDegreeOfParallelism = DefaultParallelOptions.MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (sourceFile, token) =>
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var destFile = Path.Combine(destDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                await using var sourceStream = File.OpenRead(sourceFile);
                await using var destinationStream = File.Create(destFile);
                await sourceStream.CopyToAsync(destinationStream, token);
            }
        );
    }
}
