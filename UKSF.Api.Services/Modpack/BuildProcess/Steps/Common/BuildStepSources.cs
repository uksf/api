using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepSources : BuildStep {
        public const string NAME = "Sources";
        private string gitPath;

        protected override async Task ProcessExecute() {
            Logger.Log("Checking out latest sources");
            gitPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_GIT_PATH").AsString();

            await CheckoutStaticSource("ACE", "ace", "uksfcustom");
            await CheckoutStaticSource("ACRE", "acre", "customrelease");
            await CheckoutStaticSource("UKSF F-35", "f35", "master");
            await CheckoutModpack();
        }

        private async Task CheckoutStaticSource(string name, string directoryName, string branchName) {
            Logger.LogSurround($"\nChecking out latest {name}");
            string path = Path.Join(GetBuildSourcesPath(), directoryName);
            DirectoryInfo directory = new DirectoryInfo(path);
            if (!directory.Exists) {
                throw new Exception($"{name} source directory does not exist. {name} should be cloned before running a build.");
            }

            bool updated;
            string releasePath = Path.Join(GetBuildSourcesPath(), directoryName, "release");
            DirectoryInfo release = new DirectoryInfo(releasePath);

            PSDataCollection<PSObject> results = await BuildProcessHelper.RunPowershell(
                Logger,
                CancellationTokenSource.Token,
                path,
                new List<string> { GitCommand("fetch"), GitCommand($"checkout {branchName}"), GitCommand("rev-parse HEAD") },
                true,
                false,
                true
            );
            string before = results.First().BaseObject.ToString();

            results = await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, path, new List<string> { GitCommand("pull"), GitCommand("rev-parse HEAD") }, true, false, true);
            string after = results.First().BaseObject.ToString();

            if (release.Exists) {
                Logger.Log($"{before} vs {after}");
                updated = !string.Equals(before, after);
            } else {
                Logger.Log("No release directory, will build");
                updated = true;
            }

            SetEnvironmentVariable($"{directoryName}_updated", updated);
            Logger.LogSurround($"Checked out latest {name}{(updated ? "" : " (No Changes)")}");
        }

        private async Task CheckoutModpack() {
            string referenceName = string.Equals(Build.commit.branch, "None") ? $"{Build.commit.after}" : $"latest {Build.commit.branch}";
            Logger.LogSurround($"\nChecking out {referenceName}");
            string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            DirectoryInfo modpack = new DirectoryInfo(modpackPath);
            if (!modpack.Exists) {
                throw new Exception("Modpack source directory does not exist. Modpack should be cloned before running a build.");
            }

            await BuildProcessHelper.RunPowershell(
                Logger,
                CancellationTokenSource.Token,
                modpackPath,
                new List<string> { GitCommand("reset --hard HEAD"), GitCommand("git clean -d -f"), GitCommand("fetch"), GitCommand($"checkout {Build.commit.after}"), GitCommand("pull") },
                true,
                false,
                true
            );

            Logger.LogSurround($"Checked out {referenceName}");
        }

        private string GitCommand(string command) => $".\"{gitPath}\" {command}";
    }
}
