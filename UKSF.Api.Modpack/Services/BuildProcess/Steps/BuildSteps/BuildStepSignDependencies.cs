﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepSignDependencies : FileBuildStep {
        public const string NAME = "Signatures";
        private string dsCreateKey;
        private string dsSignFile;
        private string keyName;

        protected override async Task SetupExecute() {
            dsSignFile = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSSignFile.exe");
            dsCreateKey = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSCreateKey.exe");
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
            BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, true);
            processHelper.Run(keygenPath, dsCreateKey, keyName, (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
            Logger.Log($"Created {keyName}");
            await CopyFiles(keygen, keys, new List<FileInfo> { new FileInfo(Path.Join(keygenPath, $"{keyName}.bikey")) });
            Logger.LogSurround("Created key");
        }

        protected override async Task ProcessExecute() {
            string addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
            string interceptPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept", "addons");
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

            List<FileInfo> interceptFiles = GetDirectoryContents(intercept, "*.pbo");
            Logger.LogSurround("\nSigning intercept...");
            await SignFiles(keygenPath, addonsPath, interceptFiles);
            Logger.LogSurround("Signed intercept");
        }

        private string GetKeyname() {
            return Build.environment switch {
                GameEnvironment.RELEASE => $"uksf_dependencies_{Build.version}",
                GameEnvironment.RC      => $"uksf_dependencies_{Build.version}_rc{Build.buildNumber}",
                GameEnvironment.DEV     => $"uksf_dependencies_dev_{Build.buildNumber}",
                _                       => throw new ArgumentException("Invalid build environment")
            };
        }

        private Task SignFiles(string keygenPath, string addonsPath, IReadOnlyCollection<FileInfo> files) {
            string privateKey = Path.Join(keygenPath, $"{keyName}.biprivatekey");
            int signed = 0;
            int total = files.Count;

            return BatchProcessFiles(
                files,
                10,
                file => {
                    BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, true);
                    processHelper.Run(addonsPath, dsSignFile, $"\"{privateKey}\" \"{file.FullName}\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
                    Interlocked.Increment(ref signed);
                    return Task.CompletedTask;
                },
                () => $"Signed {signed} of {total} files",
                "Failed to sign file"
            );

            // foreach (FileInfo file in files) {
            //     try {
            //         BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, true);
            //         processHelper.Run(addonsPath, dsSignFile, $"\"{privateKey}\" \"{file.FullName}\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
            //         signed++;
            //         Logger.LogInline($"Signed {signed} of {total} files");
            //     } catch (OperationCanceledException) {
            //         throw;
            //     } catch (Exception exception) {
            //         throw new Exception($"Failed to sign file '{file}'\n{exception.Message}{(exception.InnerException != null ? $"\n{exception.InnerException.Message}" : "")}", exception);
            //     }
            // }
            //
            // return Task.CompletedTask;
        }
    }
}