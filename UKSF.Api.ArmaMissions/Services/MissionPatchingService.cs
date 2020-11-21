using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.ArmaMissions.Services {
    public interface IMissionPatchingService {
        Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators);
    }

    public class MissionPatchingService : IMissionPatchingService {
        private const string EXTRACT_PBO = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\ExtractPboDos.exe";
        private const string MAKE_PBO = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\MakePbo.exe";
        private readonly ILogger _logger;

        private readonly MissionService _missionService;
        private readonly IVariablesService _variablesService;
        private string _filePath;
        private string _folderPath;
        private string _parentFolderPath;

        public MissionPatchingService(MissionService missionService, IVariablesService variablesService, ILogger logger) {
            _missionService = missionService;
            _variablesService = variablesService;
            _logger = logger;
        }

        public Task<MissionPatchingResult> PatchMission(string path, string armaServerModsPath, int armaServerDefaultMaxCurators) {
            return Task.Run(
                async () => {
                    _filePath = path;
                    _parentFolderPath = Path.GetDirectoryName(_filePath);
                    MissionPatchingResult result = new();
                    try {
                        CreateBackup();
                        UnpackPbo();
                        Mission mission = new(_folderPath);
                        result.Reports = _missionService.ProcessMission(mission, armaServerModsPath, armaServerDefaultMaxCurators);

                        await PackPbo();
                        result.PlayerCount = mission.PlayerCount;
                        result.Success = result.Reports.All(x => !x.Error);
                    } catch (Exception exception) {
                        _logger.LogError(exception);
                        result.Reports = new List<MissionPatchingReport> { new(exception) };
                        result.Success = false;
                    } finally {
                        Cleanup();
                    }

                    return result;
                }
            );
        }

        private void CreateBackup() {
            string backupPath = Path.Combine(_variablesService.GetVariable("MISSIONS_BACKUPS").AsString(), Path.GetFileName(_filePath) ?? throw new FileNotFoundException());

            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? throw new DirectoryNotFoundException());
            File.Copy(_filePath, backupPath, true);
            if (!File.Exists(backupPath)) {
                throw new FileNotFoundException();
            }
        }

        private void UnpackPbo() {
            if (Path.GetExtension(_filePath) != ".pbo") {
                throw new FileLoadException("File is not a pbo");
            }

            _folderPath = Path.Combine(_parentFolderPath, Path.GetFileNameWithoutExtension(_filePath) ?? throw new FileNotFoundException());
            if (Directory.Exists(_folderPath)) {
                Directory.Delete(_folderPath, true);
            }

            Process process = new() { StartInfo = { FileName = EXTRACT_PBO, Arguments = $"-D -P \"{_filePath}\"", UseShellExecute = false, CreateNoWindow = true } };
            process.Start();
            process.WaitForExit();

            if (!Directory.Exists(_folderPath)) {
                throw new DirectoryNotFoundException("Could not find unpacked pbo");
            }
        }

        private async Task PackPbo() {
            if (Directory.Exists(_filePath)) {
                _filePath += ".pbo";
            }

            Process process = new() {
                StartInfo = {
                    FileName = MAKE_PBO,
                    WorkingDirectory = _variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString(),
                    Arguments = $"-Z -BD -P -X=\"thumbs.db,*.txt,*.h,*.dep,*.cpp,*.bak,*.png,*.log,*.pew\" \"{_folderPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string errorOutput = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (File.Exists(_filePath)) return;
            List<string> outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
            output = outputLines.Where(x => !string.IsNullOrEmpty(x) && !x.ContainsIgnoreCase("compressing")).Aggregate((x, y) => $"{x}\n{y}");
            throw new Exception(output);
        }

        private void Cleanup() {
            try {
                Directory.Delete(_folderPath, true);
            } catch (Exception) {
                // ignore
            }
        }
    }
}
