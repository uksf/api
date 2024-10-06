namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepMerge : GitBuildStep
{
    public const string Name = "Merge";

    protected override Task ProcessExecute()
    {
        try
        {
            // Necessary to get around branch protection rules for main. Runs locally on server using user stored login as credentials
            var modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
            GitCommand(modpackPath, "git fetch");
            GitCommand(modpackPath, "git checkout -t origin/release");
            GitCommand(modpackPath, "git checkout release");
            GitCommand(modpackPath, "git pull");
            GitCommand(modpackPath, "git checkout -t origin/main");
            GitCommand(modpackPath, "git checkout main");
            GitCommand(modpackPath, "git pull");
            GitCommand(modpackPath, "git merge release");
            GitCommand(modpackPath, "git push -u origin main");
            StepLogger.Log("Release branch merge to main complete");
        }
        catch (Exception exception)
        {
            Warning($"Release branch merge to main failed:\n{exception}");
        }

        return Task.CompletedTask;
    }
}
