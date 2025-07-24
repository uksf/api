namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepMerge : GitBuildStep
{
    public const string Name = "Merge";

    protected override async Task ProcessExecute()
    {
        try
        {
            // Necessary to get around branch protection rules for main. Runs locally on server using user stored login as credentials
            var modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            var gitCommand = GitService.CreateGitCommand(modpackPath);
            await gitCommand.Execute("fetch", cancellationToken: CancellationTokenSource.Token);

            await gitCommand.Execute("checkout -t origin/release", ignoreErrors: true, cancellationToken: CancellationTokenSource.Token);
            await gitCommand.Execute("checkout release", cancellationToken: CancellationTokenSource.Token);

            await gitCommand.Execute("pull", cancellationToken: CancellationTokenSource.Token);

            await gitCommand.Execute("checkout -t origin/main", ignoreErrors: true, cancellationToken: CancellationTokenSource.Token);
            await gitCommand.Execute("checkout main", cancellationToken: CancellationTokenSource.Token);

            await gitCommand.Execute("pull", cancellationToken: CancellationTokenSource.Token);

            await gitCommand.Execute("merge release", cancellationToken: CancellationTokenSource.Token);
            await gitCommand.Execute("push -u origin main", cancellationToken: CancellationTokenSource.Token);
            StepLogger.Log("Release branch merge to main complete");
        }
        catch (Exception exception)
        {
            Warning($"Release branch merge to main failed:\n{exception}");
        }
    }
}
