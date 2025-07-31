using UKSF.Api.Core.Processes;

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
            var gitCommand = GitService.CreateGitCommand().WithWorkingDirectory(modpackPath).WithCancellationToken(CancellationTokenSource.Token);
            await gitCommand.Execute("fetch");

            var quietGitCommand = GitService.CreateGitCommand()
                                            .WithWorkingDirectory(modpackPath)
                                            .WithCancellationToken(CancellationTokenSource.Token)
                                            .WithQuiet(true)
                                            .WithAllowedExitCodes([GitExitCodes.AlreadyOnBranch]);
            await quietGitCommand.Execute("checkout -t origin/release");
            await quietGitCommand.Execute("checkout release");

            await gitCommand.Execute("pull");

            await quietGitCommand.Execute("checkout -t origin/main");
            await quietGitCommand.Execute("checkout main");

            await gitCommand.Execute("pull");

            await gitCommand.Execute("merge release");
            await gitCommand.Execute("push -u origin main");
            StepLogger.Log("Release branch merge to main complete");
        }
        catch (Exception exception)
        {
            Warning($"Release branch merge to main failed:\n{exception}");
        }
    }
}
