using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.BuildProcess.Steps;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;
using UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;
using UKSF.Api.Modpack.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IBuildStepService
{
    void RegisterBuildSteps();
    List<ModpackBuildStep> GetSteps(GameEnvironment environment);
    List<ModpackBuildStep> GetStepsForReleaseRestore();
    IBuildStep ResolveBuildStep(string buildStepName);
}

public class BuildStepService : IBuildStepService
{
    private Dictionary<string, Type> _buildStepDictionary = new();

    public void RegisterBuildSteps()
    {
        _buildStepDictionary = AppDomain.CurrentDomain.GetAssemblies()
                                        .SelectMany(x => x.GetTypes(), (_, type) => new { type })
                                        .Select(x => new { x.type, attributes = x.type.GetCustomAttributes(typeof(BuildStepAttribute), true) })
                                        .Where(x => x.attributes.Length > 0)
                                        .Select(x => new { Key = x.attributes.Cast<BuildStepAttribute>().First().Name, Value = x.type })
                                        .ToDictionary(x => x.Key, x => x.Value);
    }

    public List<ModpackBuildStep> GetSteps(GameEnvironment environment)
    {
        var steps = environment switch
        {
            GameEnvironment.Release     => GetStepsForRelease(),
            GameEnvironment.Rc          => GetStepsForRc(),
            GameEnvironment.Development => GetStepsForBuild(),
            _                           => throw new ArgumentException("Invalid build environment")
        };
        ResolveIndices(steps);
        return steps;
    }

    public List<ModpackBuildStep> GetStepsForReleaseRestore()
    {
        return [new ModpackBuildStep(BuildStepRestore.Name), new ModpackBuildStep(BuildStepUnlockServerControl.Name)];
    }

    public IBuildStep ResolveBuildStep(string buildStepName)
    {
        if (!_buildStepDictionary.ContainsKey(buildStepName))
        {
            throw new NullReferenceException($"Build step '{buildStepName}' does not exist in build step dictionary");
        }

        var type = _buildStepDictionary[buildStepName];
        var step = Activator.CreateInstance(type) as IBuildStep;
        return step;
    }

    private static List<ModpackBuildStep> GetStepsForBuild()
    {
        return
        [
            new ModpackBuildStep(BuildStepPrep.Name),
            new ModpackBuildStep(BuildStepClean.Name),
            new ModpackBuildStep(BuildStepSources.Name),
            new ModpackBuildStep(BuildStepBuildAce.Name),
            new ModpackBuildStep(BuildStepBuildAcre.Name),
            new ModpackBuildStep(BuildStepBuildAir.Name),
            new ModpackBuildStep(BuildStepBuildModpack.Name),
            new ModpackBuildStep(BuildStepIntercept.Name),
            new ModpackBuildStep(BuildStepExtensions.Name),
            new ModpackBuildStep(BuildStepSignDependencies.Name),
            new ModpackBuildStep(BuildStepDeploy.Name),
            new ModpackBuildStep(BuildStepKeys.Name),
            new ModpackBuildStep(BuildStepCbaSettings.Name),
            new ModpackBuildStep(BuildStepBuildRepo.Name)
        ];
    }

    private static List<ModpackBuildStep> GetStepsForRc()
    {
        return
        [
            new ModpackBuildStep(BuildStepPrep.Name),
            new ModpackBuildStep(BuildStepClean.Name),
            new ModpackBuildStep(BuildStepSources.Name),
            new ModpackBuildStep(BuildStepBuildAce.Name),
            new ModpackBuildStep(BuildStepBuildAcre.Name),
            new ModpackBuildStep(BuildStepBuildAir.Name),
            new ModpackBuildStep(BuildStepBuildModpack.Name),
            new ModpackBuildStep(BuildStepIntercept.Name),
            new ModpackBuildStep(BuildStepExtensions.Name),
            new ModpackBuildStep(BuildStepSignDependencies.Name),
            new ModpackBuildStep(BuildStepDeploy.Name),
            new ModpackBuildStep(BuildStepKeys.Name),
            new ModpackBuildStep(BuildStepCbaSettings.Name),
            new ModpackBuildStep(BuildStepBuildRepo.Name)
        ];
    }

    private static List<ModpackBuildStep> GetStepsForRelease()
    {
        return
        [
            new ModpackBuildStep(BuildStepClean.Name),
            new ModpackBuildStep(BuildStepLockServerControl.Name),
            new ModpackBuildStep(BuildStepBackup.Name),
            new ModpackBuildStep(BuildStepDeploy.Name),
            new ModpackBuildStep(BuildStepReleaseKeys.Name),
            new ModpackBuildStep(BuildStepCbaSettings.Name),
            new ModpackBuildStep(BuildStepBuildRepo.Name),
            new ModpackBuildStep(BuildStepPublish.Name),
            new ModpackBuildStep(BuildStepNotify.Name),
            new ModpackBuildStep(BuildStepMerge.Name),
            new ModpackBuildStep(BuildStepUnlockServerControl.Name)
        ];
    }

    private static void ResolveIndices(IReadOnlyList<ModpackBuildStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            steps[i].Index = i;
        }
    }
}
