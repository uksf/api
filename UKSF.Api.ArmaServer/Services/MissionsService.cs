using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IMissionsService
{
    List<MissionFile> GetActiveMissions();
    List<MissionFile> GetArchivedMissions();
    Task<string> UploadMissionFile(IFormFile file);
    Task<MissionPatchingResult> PatchMissionFile(string missionName);
    string FindMissionFilePath(string fileName);
    void DeleteMissionFile(string fileName);
    void ArchiveMissionFile(string fileName);
    void RestoreMissionFile(string fileName);
    FileStream GetMissionFileStream(string fileName);
}

public class MissionsService(IMissionPatchingService missionPatchingService, IGameServerHelpers gameServerHelpers) : IMissionsService
{
    private string GetActiveMissionsPath() => gameServerHelpers.GetGameServerMissionsPath();

    private string GetArchivedMissionsPath() => gameServerHelpers.GetGameServerMissionsArchivePath();

    public List<MissionFile> GetActiveMissions() => GetMissionsFromPath(GetActiveMissionsPath());

    public List<MissionFile> GetArchivedMissions() => GetMissionsFromPath(GetArchivedMissionsPath());

    public async Task<string> UploadMissionFile(IFormFile file)
    {
        var fileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(GetActiveMissionsPath(), fileName);
        await using FileStream stream = new(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return fileName;
    }

    public async Task<MissionPatchingResult> PatchMissionFile(string missionName)
    {
        var sanitizedName = Path.GetFileName(missionName);
        var missionPath = Path.Combine(GetActiveMissionsPath(), sanitizedName);
        return await missionPatchingService.PatchMission(
            missionPath,
            gameServerHelpers.GetGameServerModsPaths(GameEnvironment.Release),
            gameServerHelpers.GetMaxCuratorCountFromSettings()
        );
    }

    public string FindMissionFilePath(string fileName)
    {
        var sanitizedName = Path.GetFileName(fileName);

        var activePath = Path.Combine(GetActiveMissionsPath(), sanitizedName);
        if (File.Exists(activePath))
        {
            return activePath;
        }

        var archivePath = Path.Combine(GetArchivedMissionsPath(), sanitizedName);
        if (File.Exists(archivePath))
        {
            return archivePath;
        }

        return null;
    }

    public void DeleteMissionFile(string fileName)
    {
        var path = FindMissionFilePath(fileName) ?? throw new FileNotFoundException($"Mission file '{fileName}' not found");
        File.Delete(path);
    }

    public void ArchiveMissionFile(string fileName)
    {
        var sanitizedName = Path.GetFileName(fileName);
        var sourcePath = Path.Combine(GetActiveMissionsPath(), sanitizedName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Active mission file '{fileName}' not found");
        }

        var archiveDir = GetArchivedMissionsPath();
        Directory.CreateDirectory(archiveDir);
        var destPath = Path.Combine(archiveDir, sanitizedName);
        File.Move(sourcePath, destPath, overwrite: true);
    }

    public void RestoreMissionFile(string fileName)
    {
        var sanitizedName = Path.GetFileName(fileName);
        var sourcePath = Path.Combine(GetArchivedMissionsPath(), sanitizedName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Archived mission file '{fileName}' not found");
        }

        var activeDir = GetActiveMissionsPath();
        Directory.CreateDirectory(activeDir);
        var destPath = Path.Combine(activeDir, sanitizedName);
        File.Move(sourcePath, destPath, overwrite: true);
    }

    public FileStream GetMissionFileStream(string fileName)
    {
        var path = FindMissionFilePath(fileName) ?? throw new FileNotFoundException($"Mission file '{fileName}' not found");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static List<MissionFile> GetMissionsFromPath(string path)
    {
        if (!Directory.Exists(path))
        {
            return [];
        }

        return new DirectoryInfo(path).EnumerateFiles("*.pbo", SearchOption.TopDirectoryOnly)
                                      .Select(fileInfo => new MissionFile(fileInfo))
                                      .OrderBy(x => x.Map)
                                      .ThenBy(x => x.Name)
                                      .ToList();
    }
}
