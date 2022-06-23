using System.IO;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common
{
    [BuildStep(Name)]
    public class BuildStepCbaSettings : FileBuildStep
    {
        public const string Name = "CBA Settings";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Updating CBA settings");

            string sourceUserconfigPath;
            string targetUserconfigPath;
            if (Build.Environment == GameEnvironment.RELEASE)
            {
                sourceUserconfigPath = Path.Join(GetServerEnvironmentPath(GameEnvironment.RC), "userconfig");
                targetUserconfigPath = Path.Join(GetServerEnvironmentPath(GameEnvironment.RELEASE), "userconfig");
            }
            else
            {
                sourceUserconfigPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf");
                targetUserconfigPath = Path.Join(GetServerEnvironmentPath(Build.Environment), "userconfig");
            }

            FileInfo cbaSettingsFile = new(Path.Join(sourceUserconfigPath, "cba_settings.sqf"));

            StepLogger.LogSurround("\nCopying cba_settings.sqf...");
            await CopyFiles(new DirectoryInfo(sourceUserconfigPath), new DirectoryInfo(targetUserconfigPath), new() { cbaSettingsFile });
            StepLogger.LogSurround("Copied cba_settings.sqf");
        }
    }
}
