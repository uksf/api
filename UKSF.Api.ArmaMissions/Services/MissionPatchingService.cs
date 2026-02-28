using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public interface IMissionPatchingService
{
    Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators);
}

public class MissionPatchingService(MissionService missionService, IVariablesService variablesService, IPboTools pboTools, IUksfLogger logger)
    : IMissionPatchingService
{
    private string _filePath;
    private string _folderPath;
    private string _parentFolderPath;

    public async Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators)
    {
        _filePath = path.Replace("\"", "");
        _parentFolderPath = Path.GetDirectoryName(_filePath);

        MissionPatchingResult result = new();
        try
        {
            CreateBackup();
            await UnpackPbo();
            Mission mission = new(_folderPath);
            result.Reports = await missionService.ProcessMission(mission, armaServerModsPath, armaServerDefaultMaxCurators);
            result.PlayerCount = mission.PlayerCount;
            result.Success = result.Reports.All(x => !x.Error);

            if (!result.Success)
            {
                return result;
            }

            if (MissionUtilities.CheckFlag(mission, "missionUseSimplePack"))
            {
                logger.LogAudit($"Mission processed with simple packing enabled ({Path.GetFileName(path)})");
                await SimplePackPbo();
            }
            else
            {
                await MakePbo();
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            result.Reports = [new ValidationReport(exception)];
            result.Success = false;
        }
        finally
        {
            Cleanup();
        }

        return result;
    }

    private void CreateBackup()
    {
        var backupPath = Path.Combine(
            variablesService.GetVariable("MISSIONS_BACKUPS").AsString(),
            Path.GetFileName(_filePath) ?? throw new FileNotFoundException()
        );

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? throw new DirectoryNotFoundException());
        File.Copy(_filePath, backupPath, true);
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Could not create backup", backupPath);
        }
    }

    private async Task UnpackPbo()
    {
        if (Path.GetExtension(_filePath) != ".pbo")
        {
            throw new FileLoadException("File is not a pbo");
        }

        _folderPath = Path.Combine(_parentFolderPath, Path.GetFileNameWithoutExtension(_filePath));
        await pboTools.ExtractPbo(_filePath, _parentFolderPath);
    }

    private async Task MakePbo()
    {
        if (Directory.Exists(_filePath))
        {
            _filePath += ".pbo";
        }

        await pboTools.MakePbo(_folderPath, _filePath, variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString());
    }

    private async Task SimplePackPbo()
    {
        if (Directory.Exists(_filePath))
        {
            _filePath += ".pbo";
        }

        await pboTools.SimplePackPbo(_folderPath, _filePath, variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString());
    }

    private void Cleanup()
    {
        try
        {
            if (_folderPath is not null)
            {
                Directory.Delete(_folderPath, true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Temp directory cleanup is best-effort
        }
    }
}
