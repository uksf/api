using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps
{
    [BuildStep(NAME)]
    public class BuildStepSources : GitBuildStep
    {
        public const string NAME = "Sources";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Checking out latest sources");

            await CheckoutStaticSource("ACE", "ace", "@ace", "@uksf_ace", "uksfcustom");
            await CheckoutStaticSource("ACRE", "acre", "@acre2", "@acre2", "customrelease");
            await CheckoutStaticSource("UKSF Air", "uksf_air", "@uksf_air", "@uksf_air", "main");
            await CheckoutModpack();
        }

        private Task CheckoutStaticSource(string displayName, string modName, string releaseName, string repoName, string branchName)
        {
            StepLogger.LogSurround($"\nChecking out latest {displayName}...");

            var path = Path.Join(GetBuildSourcesPath(), modName);
            DirectoryInfo directory = new(path);
            if (!directory.Exists)
            {
                throw new($"{displayName} source directory does not exist. {displayName} should be cloned before running a build.");
            }

            var releasePath = Path.Join(GetBuildSourcesPath(), modName, "release", releaseName);
            var repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo", repoName);
            DirectoryInfo release = new(releasePath);
            DirectoryInfo repo = new(repoPath);

            GitCommand(path, "git reset --hard HEAD && git clean -d -f && git fetch");
            GitCommand(path, $"git checkout -t origin/{branchName}");
            GitCommand(path, $"git checkout {branchName}");
            var before = GitCommand(path, "git rev-parse HEAD");
            GitCommand(path, "git pull");
            var after = GitCommand(path, "git rev-parse HEAD");

            var forceBuild = GetEnvironmentVariable<bool>($"{modName}_updated");
            bool updated;
            if (!release.Exists || !repo.Exists)
            {
                StepLogger.Log("No release or repo directory, will build");
                updated = true;
            }
            else if (forceBuild)
            {
                StepLogger.Log("Force build");
                updated = true;
            }
            else
            {
                StepLogger.Log($"{before[..7]} vs {after[..7]}");
                updated = !string.Equals(before, after);
            }

            SetEnvironmentVariable($"{modName}_updated", updated);
            StepLogger.LogSurround($"Checked out latest {displayName}{(updated ? "" : " (No Changes)")}");

            return Task.CompletedTask;
        }

        private Task CheckoutModpack()
        {
            var reference = string.Equals(Build.Commit.Branch, "None") ? Build.Commit.After : Build.Commit.Branch.Replace("refs/heads/", "");
            var referenceName = string.Equals(Build.Commit.Branch, "None") ? reference : $"latest {reference}";
            StepLogger.LogSurround("\nChecking out modpack...");
            var modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            DirectoryInfo modpack = new(modpackPath);
            if (!modpack.Exists)
            {
                throw new("Modpack source directory does not exist. Modpack should be cloned before running a build.");
            }

            StepLogger.Log($"Checking out {referenceName}");
            GitCommand(modpackPath, "git reset --hard HEAD && git clean -d -f && git fetch");
            GitCommand(modpackPath, $"git checkout -t origin/{reference}");
            GitCommand(modpackPath, $"git checkout {reference}");
            GitCommand(modpackPath, "git pull");
            StepLogger.LogSurround("Checked out modpack");

            return Task.CompletedTask;
        }
    }
}
