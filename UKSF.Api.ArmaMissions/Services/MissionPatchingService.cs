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
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Extensions;

namespace UKSF.Api.ArmaMissions.Services {
    public interface IMissionPatchingService {
        Task<MissionPatchingResult> PatchMission(string path);
    }

    public class MissionPatchingService : IMissionPatchingService {
        private const string EXTRACT_PBO = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\ExtractPboDos.exe";
        private const string MAKE_PBO = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\MakePbo.exe";

        private readonly MissionService missionService;
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;

        private string filePath;
        private string folderPath;
        private string parentFolderPath;

        public MissionPatchingService(MissionService missionService, IVariablesService variablesService, ILogger logger) {
            this.missionService = missionService;
            this.variablesService = variablesService;
            this.logger = logger;
        }

        public Task<MissionPatchingResult> PatchMission(string path) {
            return Task.Run(
                async () => {
                    filePath = path;
                    parentFolderPath = Path.GetDirectoryName(filePath);
                    MissionPatchingResult result = new MissionPatchingResult();
                    try {
                        CreateBackup();
                        UnpackPbo();
                        Mission mission = new Mission(folderPath);
                        result.reports = missionService.ProcessMission(mission);

                        await PackPbo();
                        result.playerCount = mission.playerCount;
                        result.success = result.reports.All(x => !x.error);
                    } catch (Exception exception) {
                        logger.LogError(exception);
                        result.reports = new List<MissionPatchingReport> { new MissionPatchingReport(exception) };
                        result.success = false;
                    } finally {
                        Cleanup();
                    }

                    return result;
                }
            );
        }

        private void CreateBackup() {
            string backupPath = Path.Combine(variablesService.GetVariable("MISSIONS_BACKUPS").AsString(), Path.GetFileName(filePath) ?? throw new FileNotFoundException());

            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? throw new DirectoryNotFoundException());
            File.Copy(filePath, backupPath, true);
            if (!File.Exists(backupPath)) {
                throw new FileNotFoundException();
            }
        }

        private void UnpackPbo() {
            if (Path.GetExtension(filePath) != ".pbo") {
                throw new FileLoadException("File is not a pbo");
            }

            folderPath = Path.Combine(parentFolderPath, Path.GetFileNameWithoutExtension(filePath) ?? throw new FileNotFoundException());
            if (Directory.Exists(folderPath)) {
                Directory.Delete(folderPath, true);
            }

            Process process = new Process { StartInfo = { FileName = EXTRACT_PBO, Arguments = $"-D -P \"{filePath}\"", UseShellExecute = false, CreateNoWindow = true } };
            process.Start();
            process.WaitForExit();

            if (!Directory.Exists(folderPath)) {
                throw new DirectoryNotFoundException("Could not find unpacked pbo");
            }
        }

        private async Task PackPbo() {
            if (Directory.Exists(filePath)) {
                filePath += ".pbo";
            }

            Process process = new Process {
                StartInfo = {
                    FileName = MAKE_PBO,
                    WorkingDirectory = variablesService.GetVariable("MISSIONS_WORKING_DIR").AsString(),
                    Arguments = $"-Z -BD -P -X=\"thumbs.db,*.txt,*.h,*.dep,*.cpp,*.bak,*.png,*.log,*.pew\" \"{folderPath}\"",
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

            if (File.Exists(filePath)) return;
            List<string> outputLines = Regex.Split($"{output}\n{errorOutput}", "\r\n|\r|\n").ToList();
            output = outputLines.Where(x => !string.IsNullOrEmpty(x) && !x.ContainsIgnoreCase("compressing")).Aggregate((x, y) => $"{x}\n{y}");
            throw new Exception(output);
        }

        private void Cleanup() {
            try {
                Directory.Delete(folderPath, true);
            } catch (Exception) {
                // ignore
            }
        }
    }
}
