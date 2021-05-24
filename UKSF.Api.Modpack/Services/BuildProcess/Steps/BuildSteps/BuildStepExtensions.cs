using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps
{
    [BuildStep(NAME)]
    public class BuildStepExtensions : FileBuildStep
    {
        public const string NAME = "Extensions";

        protected override async Task ProcessExecute()
        {
            string uksfPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf", "intercept");
            string interceptPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept");
            DirectoryInfo uksf = new(uksfPath);
            DirectoryInfo intercept = new(interceptPath);

            StepLogger.LogSurround("\nSigning extensions...");
            List<FileInfo> files = GetDirectoryContents(uksf, "*.dll").Concat(GetDirectoryContents(intercept, "*.dll")).ToList();
            await SignExtensions(files);
            StepLogger.LogSurround("Signed extensions");
        }

        private async Task SignExtensions(IReadOnlyCollection<FileInfo> files)
        {
            string certPath = Path.Join(VariablesService.GetVariable("BUILD_PATH_CERTS").AsString(), "UKSFCert.pfx");
            string signTool = VariablesService.GetVariable("BUILD_PATH_SIGNTOOL").AsString();
            int signed = 0;
            int total = files.Count;
            await BatchProcessFiles(
                files,
                2,
                file =>
                {
                    BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource, true, false, true);
                    processHelper.Run(file.DirectoryName, signTool, $"sign /f \"{certPath}\" \"{file.FullName}\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
                    Interlocked.Increment(ref signed);
                    return Task.CompletedTask;
                },
                () => $"Signed {signed} of {total} extensions",
                "Failed to sign extension"
            );
        }
    }
}
