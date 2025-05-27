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
            await GitCommand(modpackPath, "git fetch");
            await GitCommand(modpackPath, "git checkout -t origin/release");
            await GitCommand(modpackPath, "git checkout release");
            await GitCommand(modpackPath, "git pull");
            await GitCommand(modpackPath, "git checkout -t origin/main");
            await GitCommand(modpackPath, "git checkout main");
            await GitCommand(modpackPath, "git pull");
            await GitCommand(modpackPath, "git merge release");
            await GitCommand(modpackPath, "git push -u origin main");
            StepLogger.Log("Release branch merge to main complete");
        }
        catch (Exception exception)
        {
            Warning($"Release branch merge to main failed:\n{exception}");
        }
    }
}
