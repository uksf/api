using System;
using System.Collections.Generic;
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

        private Task CheckoutStaticSource(string displayName, string modName, string releaseName, string repoName, string branchName) {
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

                GitCommand(path, "git reset --hard HEAD && git clean -d -f && git fetch");
                GitCommand(path, $"git checkout {branchName}");
                string before = GitCommand(path, "git rev-parse HEAD");
                GitCommand(path, "git fetch");
                GitCommand(path, "git pull");
                string after = GitCommand(path, "git rev-parse HEAD");

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

            return Task.CompletedTask;
        }

        private Task CheckoutModpack() {
            string reference = string.Equals(Build.commit.branch, "None") ? Build.commit.after : Build.commit.branch.Replace("refs/heads/", "");
            string referenceName = string.Equals(Build.commit.branch, "None") ? reference : $"latest {reference}";
            Logger.LogSurround("\nChecking out modpack...");
            string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            DirectoryInfo modpack = new DirectoryInfo(modpackPath);
            if (!modpack.Exists) {
                throw new Exception("Modpack source directory does not exist. Modpack should be cloned before running a build.");
            }

            Logger.Log($"Checking out {referenceName}");
            GitCommand(modpackPath, "git reset --hard HEAD && git clean -d -f && git fetch");
            GitCommand(modpackPath, $"git checkout {reference}");
            GitCommand(modpackPath, "git fetch");
            GitCommand(modpackPath, "git pull");
            Logger.LogSurround("Checked out modpack");

            return Task.CompletedTask;
        }

        private string GitCommand(string workingDirectory, string command) {
            List<string> results = new BuildProcessHelper(Logger, CancellationTokenSource, false, false, true).Run(
                workingDirectory,
                "cmd.exe",
                $"/c \"{command}\"",
                (int) TimeSpan.FromSeconds(10).TotalMilliseconds
            );
            return results.Count > 0 ? results.Last() : string.Empty;
        }
    }
}
