using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModsProcessingService
{
    Task DownloadWithRetries(string workshopModId, int maxRetries = 3, CancellationToken cancellationToken = default);
    string GetWorkshopModPath(string workshopModId);
    List<string> GetModFiles(string workshopModPath);
    Task CopyPbosToDependencies(DomainWorkshopMod domainWorkshopMod, List<string> pbos, CancellationToken cancellationToken = default);
    void DeletePbosFromDependencies(List<string> pbos);
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
    public async Task DownloadWithRetries(string workshopModId, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        var retryDelay = TimeSpan.FromSeconds(5);

        var firstRoundError = await TryDownloadWithRetries(workshopModId, maxRetries, retryDelay, cancellationToken);
        if (firstRoundError == null)
        {
            return;
        }

        logger.LogWarning($"All {maxRetries} download attempts failed for {workshopModId}. Clearing workshop cache and retrying");
        ClearWorkshopCache();

        var secondRoundError = await TryDownloadWithRetries(workshopModId, maxRetries, retryDelay, cancellationToken);
        if (secondRoundError == null)
        {
            return;
        }

        throw new Exception($"Unable to download after clearing cache: {secondRoundError}");
    }

    private async Task<string> TryDownloadWithRetries(string workshopModId, int maxRetries, TimeSpan retryDelay, CancellationToken cancellationToken)
    {
        string lastError = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await steamCmdService.DownloadWorkshopMod(workshopModId);
                return null;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                if (attempt < maxRetries)
                {
                    logger.LogWarning($"Download attempt {attempt}/{maxRetries} failed for {workshopModId}: {exception.Message}");
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
        }

        return lastError;
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
        var pboFiles = Directory.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories)
                                .Select(Path.GetFileName)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();

        if (pboFiles.Count == 0)
        {
            throw new InvalidOperationException($"No PBO files found in {workshopModPath}");
        }

        var duplicates = pboFiles.GroupBy(f => f!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        if (duplicates.Count != 0)
        {
            throw new InvalidOperationException($"Duplicate PBO names found: {string.Join(", ", duplicates)}. Manual investigation required.");
        }

        logger.LogInfo($"Found {pboFiles.Count} PBO files in {workshopModPath}");
        return pboFiles;
    }

    public async Task CopyPbosToDependencies(DomainWorkshopMod domainWorkshopMod, List<string> pbos, CancellationToken cancellationToken = default)
    {
        var devPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_DEV").AsString(), "Repo", "@uksf_dependencies", "addons");
        var rcPath = Path.Combine(variablesService.GetVariable("MODPACK_PATH_RC").AsString(), "Repo", "@uksf_dependencies", "addons");
        var workshopModPath = GetWorkshopModPath(domainWorkshopMod.SteamId);

        var allPboFiles = Directory.EnumerateFiles(workshopModPath, "*.pbo", SearchOption.AllDirectories)
                                   .ToDictionary(path => Path.GetFileName(path)!, path => path, StringComparer.OrdinalIgnoreCase);

        var copyTasks = new List<Task>();
        foreach (var pboName in pbos)
        {
            var sourcePath = allPboFiles[pboName];
            copyTasks.Add(CopyFileAsync(sourcePath, Path.Combine(devPath, pboName), cancellationToken));
            copyTasks.Add(CopyFileAsync(sourcePath, Path.Combine(rcPath, pboName), cancellationToken));
        }

        await Task.WhenAll(copyTasks);
        logger.LogInfo($"Copied {pbos.Count} PBOs for {domainWorkshopMod.Id}");
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

        logger.LogInfo($"Deleted {pbos.Count} PBOs from dependencies");
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
        return;
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
            workshopMod.StatusMessage = "An error occured";
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

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        await using var sourceStream = File.OpenRead(source);
        await using var destinationStream = File.Create(destination);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }
}
