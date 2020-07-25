using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    public class BuildStepSignDependencies : FileBuildStep {
        public const string NAME = "Dependencies";

        protected override async Task ProcessExecute() {

            string sourcePath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies");
            DirectoryInfo source = new DirectoryInfo(sourcePath);

            await Logger.LogSurround("\nDeleting dependencies signatures...");
            List<FileInfo> signatures = GetDirectoryContents(source, "*.bisign");
            List<FileInfo> zsyncs = GetDirectoryContents(source, "*.bisign.zsync");
            await DeleteFiles(signatures);
            await DeleteFiles(zsyncs);
            await Logger.LogSurround("Deleted dependencies signatures");


        }

        private string GetArmaToolsPath() {
            RegistryKey gameKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\bohemia interactive\arma 3 tools");
            if (gameKey == null) return "";
            string install = gameKey.GetValue("main", "").ToString();
            return Directory.Exists(install) ? install : "";
        }
    }
}
