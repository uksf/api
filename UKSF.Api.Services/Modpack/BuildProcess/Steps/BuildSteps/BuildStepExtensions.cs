using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepExtensions : FileBuildStep {
        public const string NAME = "Extensions";

        protected override async Task ProcessExecute() {
            string uksfPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf", "intercept");
            string interceptPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept");
            DirectoryInfo uksf = new DirectoryInfo(uksfPath);
            DirectoryInfo intercept = new DirectoryInfo(interceptPath);

            Logger.LogSurround("\nSigning extensions...");
            List<FileInfo> files = GetDirectoryContents(uksf, "*.dll").Concat(GetDirectoryContents(intercept, "*.dll")).ToList();
            await SignExtensions(files);
            Logger.LogSurround("Signed extensions");
        }

        private async Task SignExtensions(IReadOnlyCollection<FileInfo> files) {
            string certPath = Path.Join(VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_CERTS").AsString(), "UKSFCert.pfx");
            string signTool = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_SIGNTOOL").AsString();
            int signed = 0;
            int total = files.Count;
            await BatchProcessFiles(
                files,
                2,
                async file => {
                    await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, file.DirectoryName, signTool, $"sign /f \"{certPath}\" \"{file.FullName}\"", TimeSpan.FromSeconds(10).Milliseconds, true);
                    Interlocked.Increment(ref signed);
                },
                () => $"Signed {signed} of {total} extensions",
                "Failed to sign extension"
            );
        }
    }
}
