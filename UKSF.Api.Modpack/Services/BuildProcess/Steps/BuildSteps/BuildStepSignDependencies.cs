using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps
{
    [BuildStep(Name)]
    public class BuildStepSignDependencies : FileBuildStep
    {
        public const string Name = "Signatures";
        private string _dsCreateKey;
        private string _dsSignFile;
        private string _keyName;

        protected override async Task SetupExecute()
        {
            _dsSignFile = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSSignFile.exe");
            _dsCreateKey = Path.Join(VariablesService.GetVariable("BUILD_PATH_DSSIGN").AsString(), "DSCreateKey.exe");
            _keyName = GetKeyname();

            var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
            var keysPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "keys");
            DirectoryInfo keygen = new(keygenPath);
            DirectoryInfo keys = new(keysPath);
            keygen.Create();
            keys.Create();

            StepLogger.LogSurround("\nClearing keys directories...");
            await DeleteDirectoryContents(keysPath);
            await DeleteDirectoryContents(keygenPath);
            StepLogger.LogSurround("Cleared keys directories");

            StepLogger.LogSurround("\nCreating key...");
            BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource, true);
            processHelper.Run(keygenPath, _dsCreateKey, _keyName, (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
            StepLogger.Log($"Created {_keyName}");
            await CopyFiles(keygen, keys, new() { new(Path.Join(keygenPath, $"{_keyName}.bikey")) });
            StepLogger.LogSurround("Created key");
        }

        protected override async Task ProcessExecute()
        {
            var addonsPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
            var interceptPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept", "addons");
            var keygenPath = Path.Join(GetBuildEnvironmentPath(), "PrivateKeys");
            DirectoryInfo addons = new(addonsPath);
            DirectoryInfo intercept = new(interceptPath);

            StepLogger.LogSurround("\nDeleting dependencies signatures...");
            await DeleteFiles(GetDirectoryContents(addons, "*.bisign*"));
            StepLogger.LogSurround("Deleted dependencies signatures");

            var repoFiles = GetDirectoryContents(addons, "*.pbo");
            StepLogger.LogSurround("\nSigning dependencies...");
            await SignFiles(keygenPath, addonsPath, repoFiles);
            StepLogger.LogSurround("Signed dependencies");

            var interceptFiles = GetDirectoryContents(intercept, "*.pbo");
            StepLogger.LogSurround("\nSigning intercept...");
            await SignFiles(keygenPath, addonsPath, interceptFiles);
            StepLogger.LogSurround("Signed intercept");
        }

        private string GetKeyname()
        {
            return Build.Environment switch
            {
                GameEnvironment.RELEASE => $"uksf_dependencies_{Build.Version}",
                GameEnvironment.RC      => $"uksf_dependencies_{Build.Version}_rc{Build.BuildNumber}",
                GameEnvironment.DEV     => $"uksf_dependencies_dev_{Build.BuildNumber}",
                _                       => throw new ArgumentException("Invalid build environment")
            };
        }

        private Task SignFiles(string keygenPath, string addonsPath, IReadOnlyCollection<FileInfo> files)
        {
            var privateKey = Path.Join(keygenPath, $"{_keyName}.biprivatekey");
            var signed = 0;
            var total = files.Count;

            return BatchProcessFiles(
                files,
                10,
                file =>
                {
                    BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource, true);
                    processHelper.Run(addonsPath, _dsSignFile, $"\"{privateKey}\" \"{file.FullName}\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);
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
