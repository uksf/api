﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepSignDependencies : FileBuildStep {
        public const string NAME = "Signatures";
        private string dsCreateKey;
        private string dsSignFile;
        private string keyName;

        protected override async Task SetupExecute() {
            dsSignFile = Path.Join(VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_DSSIGN").AsString(), "DSSignFile.exe");
            dsCreateKey = Path.Join(VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_DSSIGN").AsString(), "DSCreateKey.exe");
            keyName = GetKeyname();

            string keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "keys");
            DirectoryInfo keygen = new DirectoryInfo(keygenPath);
            DirectoryInfo keys = new DirectoryInfo(keysPath);
            keygen.Create();
            keys.Create();

            Logger.LogSurround("\nClearing keys directories...");
            await DeleteDirectoryContents(keysPath);
            await DeleteDirectoryContents(keygenPath);
            Logger.LogSurround("Cleared keys directories");

            Logger.LogSurround("\nCreating key...");
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, keygenPath, new List<string> { $".\"{dsCreateKey}\" {keyName}" });
            Logger.Log($"Created {keyName}");
            await CopyFiles(keygen, keys, new List<FileInfo> { new FileInfo(Path.Join(keygenPath, $"{keyName}.bikey")) });
            Logger.LogSurround("Created key");
        }

        protected override async Task ProcessExecute() {
            string addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
            string interceptPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@intercept", "addons");
            string keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
            DirectoryInfo addons = new DirectoryInfo(addonsPath);
            DirectoryInfo intercept = new DirectoryInfo(interceptPath);

            Logger.LogSurround("\nDeleting dependencies signatures...");
            await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
            Logger.LogSurround("Deleted dependencies signatures");

            List<FileInfo> repoFiles = GetDirectoryContents(addons, "*.pbo");
            Logger.LogSurround("\nSigning dependencies...");
            await SignFiles(keygenPath, addonsPath, repoFiles);
            Logger.LogSurround("Signed dependencies");

            List<FileInfo> interceptIiles = GetDirectoryContents(intercept, "*.pbo");
            Logger.LogSurround("\nSigning intercept...");
            await SignFiles(keygenPath, addonsPath, interceptIiles);
            Logger.LogSurround("Signed intercept");
        }

        private string GetKeyname() {
            return Build.environment switch {
                GameEnvironment.RELEASE => $"uksf_dependencies_{Build.version}",
                GameEnvironment.RC => $"uksf_dependencies_{Build.version}_rc{Build.buildNumber}",
                GameEnvironment.DEV => $"uksf_dependencies_dev_{Build.buildNumber}",
                _ => throw new ArgumentException("Invalid build environment")
            };
        }

        private async Task SignFiles(string keygenPath, string addonsPath, IReadOnlyCollection<FileInfo> files) {
            string privateKey = Path.Join(keygenPath, $"{keyName}.biprivatekey");
            int signed = 0;
            int total = files.Count;
            await ParallelProcessFiles(
                files,
                50,
                async file => {
                    await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, addonsPath, new List<string> { $".\"{dsSignFile}\" \"{privateKey}\" \"{file.FullName}\"" });
                    Interlocked.Increment(ref signed);
                },
                () => $"Signed {signed} of {total} files",
                "Failed to sign file"
            );
        }
    }
}