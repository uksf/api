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
            await GitCommand(modpackPath, "fetch");
            await GitCommand(modpackPath, "checkout -t origin/release");
            await GitCommand(modpackPath, "checkout release");
            await GitCommand(modpackPath, "pull");
            await GitCommand(modpackPath, "checkout -t origin/main");
            await GitCommand(modpackPath, "checkout main");
            await GitCommand(modpackPath, "pull");
            await GitCommand(modpackPath, "merge release");
            await GitCommand(modpackPath, "push -u origin main");
            StepLogger.Log("Release branch merge to main complete");
        }
        catch (Exception exception)
        {
            Warning($"Release branch merge to main failed:\n{exception}");
        }
    }
}
