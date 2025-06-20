using System.Diagnostics;
using System.Text.RegularExpressions;
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

public class MissionPatchingService(MissionService missionService, IVariablesService variablesService, IUksfLogger logger) : IMissionPatchingService
{
    private const string ExtractPboPath = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\ExtractPboDos.exe";
    private const string MakePboPath = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\MakePbo.exe";
    private const string SimplePackPboPath = "C:\\Program Files\\PBO Manager v.1.4 beta\\PBOConsole.exe";

    private string _filePath;
    private string _folderPath;
    private string _parentFolderPath;

    public Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators)
    {
        return Task.Run(async () =>
            {
                _filePath = path;
                _parentFolderPath = Path.GetDirectoryName(_filePath);

                MissionPatchingResult result = new();
                try
                {
                    CreateBackup();
                    UnpackPbo();
                    Mission mission = new(_folderPath);
                    result.Reports = missionService.ProcessMission(mission, armaServerModsPath, armaServerDefaultMaxCurators);
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
        );
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

    private void UnpackPbo()
    {
        if (Path.GetExtension(_filePath) != ".pbo")
        {
            throw new FileLoadException("File is not a pbo");
        }

        _folderPath = Path.Combine(_parentFolderPath, Path.GetFileNameWithoutExtension(_filePath));
        if (Directory.Exists(_folderPath))
        {
            Directory.Delete(_folderPath, true);
        }

        Process process = new()
        {
            StartInfo =
            {
                FileName = ExtractPboPath,
                Arguments = $"-D -P \"{_filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();

        if (!Directory.Exists(_folderPath))
        {
            throw new DirectoryNotFoundException("Could not find unpacked pbo");
        }
    }

    private async Task MakePbo()
    {
        if (Directory.Exists(_filePath))
        {
            _filePath += ".pbo";
        }

        Process process = new()
        {
            StartInfo =
            {
                FileName = MakePboPath,
                WorkingDirectory = variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString(),
                Arguments = $"-Z -BD -P -X=\"thumbs.db,*.txt,*.h,*.dep,*.cpp,*.bak,*.png,*.log,*.pew\" \"{_folderPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (File.Exists(_filePath))
        {
            return;
        }

        var outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
        output = outputLines.Where(x => !string.IsNullOrEmpty(x) && !x.ContainsIgnoreCase("compressing")).Aggregate((x, y) => $"{x}\n{y}");
        throw new Exception(output);
    }

    private async Task SimplePackPbo()
    {
        if (Directory.Exists(_filePath))
        {
            _filePath += ".pbo";
        }

        Process process = new()
        {
            StartInfo =
            {
                FileName = SimplePackPboPath,
                WorkingDirectory = variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString(),
                Arguments = $"-pack \"{_folderPath}\" \"{_filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (File.Exists(_filePath))
        {
            File.Delete($"{_filePath}.bak");
            return;
        }

        var outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
        output = outputLines.Where(x => !string.IsNullOrEmpty(x)).Aggregate((x, y) => $"{x}\n{y}");
        throw new Exception(output);
    }

    private void Cleanup()
    {
        try
        {
            Directory.Delete(_folderPath, true);
        }
        catch (Exception)
        {
            // ignore
        }
    }
}
