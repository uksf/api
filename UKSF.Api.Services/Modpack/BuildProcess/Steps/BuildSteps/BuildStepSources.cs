using System;
using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepSources : BuildStep {
        public const string NAME = "Sources";
        private string gitPath;

        protected override Task ProcessExecute() {
            Logger.Log("Checking out latest sources");
            gitPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_GIT").AsString();

            CheckoutStaticSource("ACE", "ace", "@ace", "@uksf_ace", "uksfcustom");
            CheckoutStaticSource("ACRE", "acre", "@acre2", "@acre2", "customrelease");
            CheckoutStaticSource("UKSF F-35", "f35", "@uksf_f35", "@uksf", "master");
            CheckoutModpack();

            return Task.CompletedTask;
        }

        private void CheckoutStaticSource(string displayName, string modName, string releaseName, string repoName, string branchName) {
            Logger.LogSurround($"\nChecking out latest {displayName}...");
            string path = Path.Join(GetBuildSourcesPath(), modName);
            DirectoryInfo directory = new DirectoryInfo(path);
            if (!directory.Exists) {
                throw new Exception($"{displayName} source directory does not exist. {displayName} should be cloned before running a build.");
            }

            bool updated;
            string releasePath = Path.Join(GetBuildSourcesPath(), modName, "release", releaseName);
            string repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo", repoName);
            DirectoryInfo release = new DirectoryInfo(releasePath);
            DirectoryInfo repo = new DirectoryInfo(repoPath);

            string before = BuildProcessHelper.RunProcess(
                Logger,
                CancellationTokenSource.Token,
                path,
                "cmd.exe",
                $"/c \"git reset --hard HEAD && git clean -d -f && git checkout {branchName} && git rev-parse HEAD\"",
                true,
                false,
                true
            );
            string after = BuildProcessHelper.RunProcess(Logger, CancellationTokenSource.Token, path, "cmd.exe", "/c \"git pull && git rev-parse HEAD\"", true, false, true);

            if (release.Exists && repo.Exists) {
                Logger.Log($"{before?.Substring(0, 7)} vs {after?.Substring(0, 7)}");
                updated = !string.Equals(before, after);
            } else {
                Logger.Log("No release or repo directory, will build");
                updated = true;
            }

            SetEnvironmentVariable($"{modName}_updated", updated);
            Logger.LogSurround($"Checked out latest {displayName}{(updated ? "" : " (No Changes)")}");
        }

        private void CheckoutModpack() {
            string reference = string.Equals(Build.commit.branch, "None") ? Build.commit.after : Build.commit.branch;
            string referenceName = string.Equals(Build.commit.branch, "None") ? reference : $"latest {reference}";
            Logger.LogSurround("\nChecking out modpack...");
            string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            DirectoryInfo modpack = new DirectoryInfo(modpackPath);
            if (!modpack.Exists) {
                throw new Exception("Modpack source directory does not exist. Modpack should be cloned before running a build.");
            }

            Logger.Log($"Checking out {referenceName}");
            BuildProcessHelper.RunProcess(
                Logger,
                CancellationTokenSource.Token,
                modpackPath,
                "cmd.exe",
                $"/c \"git reset --hard HEAD && git clean -d -f && git checkout {reference} && git pull\"",
                true,
                false,
                true
            );

            Logger.LogSurround("Checked out modpack");
        }
    }
}
