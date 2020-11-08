using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepCbaSettings : FileBuildStep {
        public const string NAME = "CBA Settings";

        protected override async Task ProcessExecute() {
            Logger.Log("Updating CBA settings");

            string sourceUserconfigPath;
            string targetUserconfigPath;
            if (Build.environment == GameEnvironment.RELEASE) {
                sourceUserconfigPath = Path.Join(GetServerEnvironmentPath(GameEnvironment.RC), "userconfig");
                targetUserconfigPath = Path.Join(GetServerEnvironmentPath(GameEnvironment.RELEASE), "userconfig");
            } else {
                sourceUserconfigPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf");
                targetUserconfigPath = Path.Join(GetServerEnvironmentPath(Build.environment), "userconfig");
            }

            FileInfo cbaSettingsFile = new FileInfo(Path.Join(sourceUserconfigPath, "cba_settings.sqf"));

            Logger.LogSurround("\nCopying cba_settings.sqf...");
            await CopyFiles(new DirectoryInfo(sourceUserconfigPath), new DirectoryInfo(targetUserconfigPath), new List<FileInfo> { cbaSettingsFile });
            Logger.LogSurround("Copied cba_settings.sqf");
        }
    }
}
