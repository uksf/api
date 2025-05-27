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

        // Break up the complex git command chain and add cancellation checks
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(path, "git reset --hard HEAD");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(path, "git clean -d -f");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(path, "git fetch");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();

        // Handle potential branch creation errors gracefully
        try
        {
            await GitCommand(path, $"git checkout -t origin/{branchName}");
        }
        catch (Exception ex)
        {
            // Branch might already exist, try to checkout directly
            StepLogger.Log($"Branch tracking creation failed, attempting direct checkout: {ex.Message}");
        }

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(path, $"git checkout {branchName}");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        var before = await GitCommand(path, "git rev-parse HEAD");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(path, "git pull");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        var after = await GitCommand(path, "git rev-parse HEAD");

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

        // Break up the complex git command chain and add cancellation checks
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(modpackPath, "git reset --hard HEAD");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(modpackPath, "git clean -d -f");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(modpackPath, "git fetch");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();

        // Handle potential branch creation errors gracefully
        try
        {
            await GitCommand(modpackPath, $"git checkout -t origin/{reference}");
        }
        catch (Exception ex)
        {
            // Branch might already exist, try to checkout directly
            StepLogger.Log($"Branch tracking creation failed, attempting direct checkout: {ex.Message}");
        }

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(modpackPath, $"git checkout {reference}");

        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        await GitCommand(modpackPath, "git pull");

        StepLogger.LogSurround("Checked out modpack");
    }
}
