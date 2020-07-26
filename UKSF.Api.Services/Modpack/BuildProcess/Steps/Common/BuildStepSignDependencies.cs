using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepSignDependencies : FileBuildStep {
        public const string NAME = "Dependencies";
        private string dsCreateKey;
        private string dsSignFile;
        private string keyName;

        protected override async Task SetupExecute() {
            dsSignFile = Path.Join(VariablesWrapper.VariablesDataService().GetSingle("BUILD_DSSIGN_PATH").AsString(), "DSSignFile.exe");
            dsCreateKey = Path.Join(VariablesWrapper.VariablesDataService().GetSingle("BUILD_DSSIGN_PATH").AsString(), "DSCreateKey.exe");
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
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, keygenPath, $".\"{dsCreateKey}\" {keyName}");
            Logger.Log($"Created {keyName}");
            await CopyFiles(keygen, keys, new List<FileInfo> { new FileInfo(Path.Join(keygenPath, $"{keyName}.bikey")) });
            Logger.LogSurround("Created key");
        }

        protected override async Task ProcessExecute() {
            string addonsPath = Path.Join(Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies"), "addons");
            string keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
            DirectoryInfo addons = new DirectoryInfo(addonsPath);

            Logger.LogSurround("\nDeleting dependencies signatures...");
            await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
            Logger.LogSurround("Deleted dependencies signatures");

            List<FileInfo> files = GetDirectoryContents(addons, "*.pbo");
            Logger.LogSurround("\nSigning dependencies...");
            await SignFiles(keygenPath, addonsPath, files);
            Logger.LogSurround("Signed dependencies");
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

            SemaphoreSlim taskLimiter = new SemaphoreSlim(100);
            IEnumerable<Task> tasks = files.Select(
                async file => {
                    if (CancellationTokenSource.Token.IsCancellationRequested) return;

                    try {
                        await taskLimiter.WaitAsync(CancellationTokenSource.Token);
                        if (CancellationTokenSource.Token.IsCancellationRequested) return;

                        await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, addonsPath, $".\"{dsSignFile}\" \"{privateKey}\" \"{file.FullName}\"");
                        Interlocked.Increment(ref signed);
                        Logger.LogInline($"Signed {signed} of {files.Count} files");
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception exception) {
                        throw new Exception($"Failed to sign file '{file}'\n{exception.Message}", exception);
                    } finally {
                        taskLimiter.Release();
                    }
                }
            );

            Logger.LogInstant($"Signed {signed} of {files.Count} files");
            await Task.WhenAll(tasks);
        }
    }
}
