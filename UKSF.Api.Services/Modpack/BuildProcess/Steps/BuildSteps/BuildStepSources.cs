using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepSources : BuildStep {
        public const string NAME = "Sources";

        protected override async Task ProcessExecute() {
            Logger.Log("Checking out latest sources");

            await CheckoutStaticSource("ACE", "ace", "@ace", "@uksf_ace", "uksfcustom");
            await CheckoutStaticSource("ACRE", "acre", "@acre2", "@acre2", "customrelease");
            await CheckoutStaticSource("UKSF F-35", "f35", "@uksf_f35", "@uksf", "master");
            await CheckoutModpack();
        }

        private async Task CheckoutStaticSource(string displayName, string modName, string releaseName, string repoName, string branchName) {
            Logger.LogSurround($"\nChecking out latest {displayName}...");

            bool forceBuild = GetEnvironmentVariable<bool>($"{modName}_updated");
            bool updated;
            if (forceBuild) {
                Logger.Log("Force build");
                updated = true;
            } else {
                string path = Path.Join(GetBuildSourcesPath(), modName);
                DirectoryInfo directory = new DirectoryInfo(path);
                if (!directory.Exists) {
                    throw new Exception($"{displayName} source directory does not exist. {displayName} should be cloned before running a build.");
                }

                string releasePath = Path.Join(GetBuildSourcesPath(), modName, "release", releaseName);
                string repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo", repoName);
                DirectoryInfo release = new DirectoryInfo(releasePath);
                DirectoryInfo repo = new DirectoryInfo(repoPath);

                await BuildProcessHelper.RunProcess(
                    Logger,
                    CancellationTokenSource,
                    path,
                    "cmd.exe",
                    $"/c \"git reset --hard HEAD && git clean -d -f && git checkout {branchName}\"",
                    TimeSpan.FromSeconds(30).TotalMilliseconds,
                    true,
                    false,
                    true
                );
                string before = (await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, path, "cmd.exe", "/c \"git rev-parse HEAD\"", TimeSpan.FromSeconds(10).TotalMilliseconds, true, false, true)).Last();
                await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, path, "cmd.exe", "/c \"git pull\"", TimeSpan.FromSeconds(30).TotalMilliseconds, true, false, true);
                string after = (await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, path, "cmd.exe", "/c \"git rev-parse HEAD\"", TimeSpan.FromSeconds(10).TotalMilliseconds, true, false, true)).Last();

                if (release.Exists && repo.Exists) {
                    Logger.Log($"{before?.Substring(0, 7)} vs {after?.Substring(0, 7)}");
                    updated = !string.Equals(before, after);
                } else {
                    Logger.Log("No release or repo directory, will build");
                    updated = true;
                }
            }

            SetEnvironmentVariable($"{modName}_updated", updated);
            Logger.LogSurround($"Checked out latest {displayName}{(updated ? "" : " (No Changes)")}");
        }

        private async Task CheckoutModpack() {
            string reference = string.Equals(Build.commit.branch, "None") ? Build.commit.after : Build.commit.branch;
            string referenceName = string.Equals(Build.commit.branch, "None") ? reference : $"latest {reference}";
            Logger.LogSurround("\nChecking out modpack...");
            string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            DirectoryInfo modpack = new DirectoryInfo(modpackPath);
            if (!modpack.Exists) {
                throw new Exception("Modpack source directory does not exist. Modpack should be cloned before running a build.");
            }

            Logger.Log($"Checking out {referenceName}");
            await BuildProcessHelper.RunProcess(
                Logger,
                CancellationTokenSource,
                modpackPath,
                "cmd.exe",
                $"/c \"git reset --hard HEAD && git clean -d -f && git checkout {reference} && git pull\"",
                TimeSpan.FromSeconds(30).TotalMilliseconds,
                true,
                false,
                true
            );

            Logger.LogSurround("Checked out modpack");
        }
    }
}
