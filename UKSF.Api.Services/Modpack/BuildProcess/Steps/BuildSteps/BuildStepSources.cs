using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepSources : BuildStep {
        public const string NAME = "Sources";
        private string gitPath;

        protected override async Task ProcessExecute() {
            Logger.Log("Checking out latest sources");
            gitPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_GIT").AsString();

            await CheckoutStaticSource("ACE", "ace", "@ace", "uksfcustom");
            await CheckoutStaticSource("ACRE", "acre", "@acre2", "customrelease");
            await CheckoutStaticSource("UKSF F-35", "f35", "@uksf_f35", "master");
            await CheckoutModpack();
        }

        private async Task CheckoutStaticSource(string displayName, string modName, string releaseName, string branchName) {
            Logger.LogSurround($"\nChecking out latest {displayName}...");
            string path = Path.Join(GetBuildSourcesPath(), modName);
            DirectoryInfo directory = new DirectoryInfo(path);
            if (!directory.Exists) {
                throw new Exception($"{displayName} source directory does not exist. {displayName} should be cloned before running a build.");
            }

            bool updated;
            string releasePath = Path.Join(GetBuildSourcesPath(), modName, "release", releaseName);
            string repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo", releaseName);
            DirectoryInfo release = new DirectoryInfo(releasePath);
            DirectoryInfo repo = new DirectoryInfo(repoPath);

            PSDataCollection<PSObject> results = await BuildProcessHelper.RunPowershell(
                Logger,
                CancellationTokenSource.Token,
                path,
                new List<string> { GitCommand("reset --hard HEAD"), GitCommand("git clean -d -f"), GitCommand("fetch"), GitCommand($"checkout {branchName}"), GitCommand("rev-parse HEAD") },
                true,
                false,
                true
            );
            string before = results.First().BaseObject.ToString();

            results = await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, path, new List<string> { GitCommand("pull"), GitCommand("rev-parse HEAD") }, true, false, true);
            string after = results.First().BaseObject.ToString();

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
            await BuildProcessHelper.RunPowershell(
                Logger,
                CancellationTokenSource.Token,
                modpackPath,
                new List<string> { GitCommand("reset --hard HEAD"), GitCommand("git clean -d -f"), GitCommand("fetch"), GitCommand($"checkout {reference}") },
                true,
                false,
                true
            );

            Logger.LogSurround("Checked out modpack");
        }

        private string GitCommand(string command) => $".\"{gitPath}\" {command}";
    }
}
