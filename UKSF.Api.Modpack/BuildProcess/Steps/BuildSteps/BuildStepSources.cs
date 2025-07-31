using UKSF.Api.Core.Processes;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepSources : GitBuildStep
{
    public const string Name = "Sources";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Checking out latest sources");

        await CheckoutStaticSource("ACE", "ace", "@ace", "@uksf_ace", "uksfcustom");
        await CheckoutStaticSource("ACRE", "acre", "@acre2", "@uksf_acre2", "customrelease");
        await CheckoutStaticSource("UKSF Air", "uksf_air", "@uksf_air", "@uksf_air", "main");
        await CheckoutModpack();
    }

    private async Task CheckoutStaticSource(string displayName, string modName, string releaseName, string repoName, string branchName)
    {
        StepLogger.LogSurround($"\nChecking out latest {displayName}...");

        var path = Path.Join(GetBuildSourcesPath(), modName);
        DirectoryInfo directory = new(path);
        if (!directory.Exists)
        {
            throw new Exception($"{displayName} source directory does not exist. {displayName} should be cloned before running a build.");
        }

        var releasePath = Path.Join(GetBuildSourcesPath(), modName, "release", releaseName);
        var repoPath = Path.Join(GetBuildEnvironmentPath(), "Repo", repoName);
        DirectoryInfo release = new(releasePath);
        DirectoryInfo repo = new(repoPath);

        var gitCommand = GitService.CreateGitCommand().WithWorkingDirectory(path).WithCancellationToken(CancellationTokenSource.Token);
        await gitCommand.Execute("reset --hard HEAD");
        await gitCommand.Execute("clean -d -f");
        await gitCommand.Execute("fetch");

        var quietGitCommand = GitService.CreateGitCommand()
                                        .WithWorkingDirectory(path)
                                        .WithCancellationToken(CancellationTokenSource.Token)
                                        .WithQuiet(true)
                                        .WithAllowedExitCodes([GitExitCodes.AlreadyOnBranch]);
        await quietGitCommand.Execute($"checkout -t origin/{branchName}");
        await quietGitCommand.Execute($"checkout {branchName}");

        var before = await gitCommand.Execute("rev-parse HEAD");
        await gitCommand.Execute("pull");
        var after = await gitCommand.Execute("rev-parse HEAD");

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
    }

    private async Task CheckoutModpack()
    {
        var reference = string.Equals(Build.Commit.Branch, "None") ? Build.Commit.After : Build.Commit.Branch.Replace("refs/heads/", "");
        var referenceName = string.Equals(Build.Commit.Branch, "None") ? reference : $"latest {reference}";
        StepLogger.LogSurround("\nChecking out modpack...");
        var modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
        DirectoryInfo modpack = new(modpackPath);
        if (!modpack.Exists)
        {
            throw new Exception("Modpack source directory does not exist. Modpack should be cloned before running a build.");
        }

        StepLogger.Log($"Checking out {referenceName}");

        var gitCommand = GitService.CreateGitCommand().WithWorkingDirectory(modpackPath).WithCancellationToken(CancellationTokenSource.Token);
        await gitCommand.Execute("reset --hard HEAD");
        await gitCommand.Execute("clean -d -f");
        await gitCommand.Execute("fetch");

        var quietGitCommand = GitService.CreateGitCommand()
                                        .WithWorkingDirectory(modpackPath)
                                        .WithCancellationToken(CancellationTokenSource.Token)
                                        .WithQuiet(true)
                                        .WithAllowedExitCodes([GitExitCodes.AlreadyOnBranch]);
        await quietGitCommand.Execute($"checkout -t origin/{reference}");
        await quietGitCommand.Execute($"checkout {reference}");

        await gitCommand.Execute("pull");

        StepLogger.LogSurround("Checked out modpack");
    }
}
