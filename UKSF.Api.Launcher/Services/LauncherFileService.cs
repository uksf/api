using System.Diagnostics;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using MongoDB.Driver;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Launcher.Context;
using UKSF.Api.Launcher.Models;

namespace UKSF.Api.Launcher.Services;

public interface ILauncherFileService
{
    Task UpdateAllVersions();
    FileStreamResult GetLauncherFile(params string[] file);
    Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files);
}

public class LauncherFileService : ILauncherFileService
{
    private readonly ILauncherFileContext _launcherFileContext;
    private readonly IVariablesService _variablesService;

    public LauncherFileService(ILauncherFileContext launcherFileContext, IVariablesService variablesService)
    {
        _launcherFileContext = launcherFileContext;
        _variablesService = variablesService;
    }

    public async Task UpdateAllVersions()
    {
        var storedVersions = _launcherFileContext.Get().ToList();
        var launcherDirectory = Path.Combine(_variablesService.GetVariable("LAUNCHER_LOCATION").AsString(), "Launcher");
        List<string> fileNames = new();
        foreach (var filePath in Directory.EnumerateFiles(launcherDirectory))
        {
            var fileName = Path.GetFileName(filePath);
            var version = FileVersionInfo.GetVersionInfo(filePath).FileVersion;
            fileNames.Add(fileName);
            var storedFile = storedVersions.FirstOrDefault(x => x.FileName == fileName);
            if (storedFile == null)
            {
                await _launcherFileContext.Add(new LauncherFile { FileName = fileName, Version = version });
                continue;
            }

            if (storedFile.Version != version)
            {
                await _launcherFileContext.Update(storedFile.Id, Builders<LauncherFile>.Update.Set(x => x.Version, version));
            }
        }

        foreach (var storedVersion in storedVersions.Where(storedVersion => fileNames.All(x => x != storedVersion.FileName)))
        {
            await _launcherFileContext.Delete(storedVersion);
        }
    }

    public FileStreamResult GetLauncherFile(params string[] file)
    {
        var paths = file.Prepend(_variablesService.GetVariable("LAUNCHER_LOCATION").AsString()).ToArray();
        var path = Path.Combine(paths);
        FileStreamResult fileStreamResult = new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), MimeUtility.GetMimeMapping(path));
        return fileStreamResult;
    }

    public async Task<Stream> GetUpdatedFiles(IEnumerable<LauncherFile> files)
    {
        var launcherDirectory = Path.Combine(_variablesService.GetVariable("LAUNCHER_LOCATION").AsString(), "Launcher");
        var storedVersions = _launcherFileContext.Get().ToList();
        List<string> updatedFiles = new();
        List<string> deletedFiles = new();
        foreach (var launcherFile in files)
        {
            var storedFile = storedVersions.FirstOrDefault(x => x.FileName == launcherFile.FileName);
            if (storedFile == null)
            {
                deletedFiles.Add(launcherFile.FileName);
                continue;
            }

            if (storedFile.Version != launcherFile.Version)
            {
                updatedFiles.Add(launcherFile.FileName);
            }
        }

        var updateFolderName = Guid.NewGuid().ToString("N");
        var updateFolder = Path.Combine(_variablesService.GetVariable("LAUNCHER_LOCATION").AsString(), updateFolderName);
        Directory.CreateDirectory(updateFolder);

        var deletedFilesPath = Path.Combine(updateFolder, "deleted");
        await File.WriteAllLinesAsync(deletedFilesPath, deletedFiles);

        foreach (var file in updatedFiles)
        {
            File.Copy(Path.Combine(launcherDirectory, file), Path.Combine(updateFolder, file), true);
        }

        var updateZipPath = Path.Combine(_variablesService.GetVariable("LAUNCHER_LOCATION").AsString(), $"{updateFolderName}.zip");
        ZipFile.CreateFromDirectory(updateFolder, updateZipPath);
        MemoryStream stream = new();
        await using (FileStream fileStream = new(updateZipPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await fileStream.CopyToAsync(stream);
        }

        File.Delete(updateZipPath);
        Directory.Delete(updateFolder, true);

        stream.Position = 0;
        return stream;
    }
}
